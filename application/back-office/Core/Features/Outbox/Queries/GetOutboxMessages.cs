using BackOffice.Database;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Outbox;

namespace BackOffice.Features.Outbox.Queries;

[PublicAPI]
public sealed record GetOutboxMessagesQuery(OutboxMessageStatus? Status = null, string? Search = null, int? PageOffset = null, int PageSize = 25)
    : IRequest<Result<OutboxMessagesResponse>>;

[PublicAPI]
public sealed record OutboxMessagesResponse(int TotalCount, int PageSize, int TotalPages, int CurrentPageOffset, OutboxMessageSummary[] Messages);

[PublicAPI]
public sealed record OutboxMessageSummary(
    Guid Id,
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
        IQueryable<OutboxMessage> messages = dbContext.OutboxMessages;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            messages = messages.Where(m => m.Type.Contains(query.Search) || (m.LastError != null && m.LastError.Contains(query.Search)));
        }

        var now = timeProvider.GetUtcNow();
        var summaries = (await messages.ToArrayAsync(cancellationToken))
            .Select(m => new OutboxMessageSummary(
                    m.Id,
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
}
