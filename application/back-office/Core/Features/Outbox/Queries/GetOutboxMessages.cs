using BackOffice.Database;
using JetBrains.Annotations;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Outbox;
using LegacyOutboxMessage = SharedKernel.Outbox.OutboxMessage;
using MassTransitOutboxMessage = MassTransit.EntityFrameworkCoreIntegration.OutboxMessage;

namespace BackOffice.Features.Outbox.Queries;

[PublicAPI]
public sealed record GetOutboxMessagesQuery(OutboxMessageStatus? Status = null, string? Search = null, int? PageOffset = null, int PageSize = 25)
    : IRequest<Result<OutboxMessagesResponse>>;

[PublicAPI]
public sealed record OutboxMessagesResponse(int TotalCount, int PageSize, int TotalPages, int CurrentPageOffset, OutboxMessageSummary[] Messages);

[PublicAPI]
public sealed record OutboxMessageSummary(
    Guid Id,
    string Source,
    string Type,
    OutboxMessageStatus Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? DeadLetteredAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? LockedUntilAt,
    string? LastError
);

public sealed class GetOutboxMessagesHandler(BackOfficeDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<GetOutboxMessagesQuery, Result<OutboxMessagesResponse>>
{
    public async Task<Result<OutboxMessagesResponse>> Handle(GetOutboxMessagesQuery query, CancellationToken cancellationToken)
    {
        IQueryable<LegacyOutboxMessage> legacyMessages = dbContext.OutboxMessages;
        IQueryable<MassTransitOutboxMessage> massTransitMessages = dbContext.Set<MassTransitOutboxMessage>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            legacyMessages = legacyMessages.Where(m => m.Type.Contains(query.Search) || (m.LastError != null && m.LastError.Contains(query.Search)));
            massTransitMessages = massTransitMessages.Where(m => m.MessageType.Contains(query.Search));
        }

        var now = timeProvider.GetUtcNow();
        var legacySummaries = (await legacyMessages.ToArrayAsync(cancellationToken))
            .Select(m => new OutboxMessageSummary(
                    m.Id,
                    "Legacy",
                    m.Type,
                    m.GetStatus(now),
                    m.Attempts,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.DeadLetteredAt,
                    m.NextAttemptAt,
                    m.LockedUntilAt,
                    m.LastError
                )
            )
            .ToArray();

        var outboxStates = await dbContext.Set<OutboxState>().ToDictionaryAsync(s => s.OutboxId, cancellationToken);
        var massTransitSummaries = (await massTransitMessages.ToArrayAsync(cancellationToken))
            .Select(m =>
                {
                    outboxStates.TryGetValue(m.OutboxId ?? Guid.Empty, out var outboxState);
                    var sentAt = ToUtcOffset(m.SentTime);
                    var enqueueAt = m.EnqueueTime is null ? sentAt : ToUtcOffset(m.EnqueueTime.Value);
                    DateTimeOffset? deliveredAt = outboxState?.Delivered is null ? null : ToUtcOffset(outboxState.Delivered.Value);
                    var status = deliveredAt is not null ? OutboxMessageStatus.Processed : enqueueAt > now ? OutboxMessageStatus.Scheduled : OutboxMessageStatus.Pending;

                    return new OutboxMessageSummary(
                        m.MessageId,
                        "MassTransit",
                        m.MessageType,
                        status,
                        0,
                        sentAt,
                        deliveredAt,
                        null,
                        enqueueAt,
                        null,
                        null
                    );
                }
            )
            .ToArray();

        var summaries = legacySummaries.Concat(massTransitSummaries).ToArray();

        if (query.Status is not null)
        {
            summaries = summaries.Where(m => m.Status == query.Status).ToArray();
        }

        summaries = summaries
            .OrderByDescending(m => m.DeadLetteredAt)
            .ThenBy(m => m.ProcessedAt is not null)
            .ThenBy(m => m.NextAttemptAt)
            .ThenByDescending(m => m.CreatedAt)
            .ToArray();

        var pageSize = query.PageSize;
        var pageOffset = query.PageOffset ?? 0;
        var totalCount = summaries.Length;
        var totalPages = totalCount == 0 ? 0 : (totalCount - 1) / pageSize + 1;
        var pagedMessages = summaries.Skip(pageOffset * pageSize).Take(pageSize).ToArray();

        return new OutboxMessagesResponse(totalCount, pageSize, totalPages, pageOffset, pagedMessages);
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
