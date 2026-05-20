using Account.Features.SupportTickets.Domain;
using Account.Features.SupportTickets.Shared;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.SupportTickets.Queries;

[PublicAPI]
public sealed record GetTicketDetailQuery(SupportTicketId Id) : IRequest<Result<TicketDetailResponse>>;

[PublicAPI]
public sealed record TicketDetailResponse(
    SupportTicketId Id,
    string ShortDisplayId,
    string Subject,
    SupportTicketCategory Category,
    SupportTicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    TicketMessageView[] Messages,
    TicketCsatView? Csat
);

[PublicAPI]
public sealed record TicketMessageView(
    SupportMessageId Id,
    SupportMessageAuthorKind AuthorKind,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAt,
    TicketAttachmentView[] Attachments
);

[PublicAPI]
public sealed record TicketAttachmentView(string FileName, string ContentType, long SizeInBytes, string Url);

[PublicAPI]
public sealed record TicketCsatView(SupportTicketCsatScore Score, string? Comment, DateTimeOffset SubmittedAt);

public sealed class GetTicketDetailHandler(ISupportTicketRepository ticketRepository, IExecutionContext executionContext)
    : IRequestHandler<GetTicketDetailQuery, Result<TicketDetailResponse>>
{
    public async Task<Result<TicketDetailResponse>> Handle(GetTicketDetailQuery query, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(query.Id, cancellationToken);
        if (ticket is null) return Result<TicketDetailResponse>.NotFound($"Support ticket with id '{query.Id}' not found.");

        if (ticket.ReporterId != executionContext.UserInfo.Id!) return Result<TicketDetailResponse>.NotFound($"Support ticket with id '{query.Id}' not found.");

        var messages = ticket.Messages
            .Where(m => m.AuthorKind != SupportMessageAuthorKind.Internal)
            .Select(m => new TicketMessageView(
                    m.Id,
                    m.AuthorKind,
                    m.AuthorDisplayName,
                    m.Body,
                    m.PostedAt,
                    m.Attachments.Select(a => new TicketAttachmentView(a.FileName, a.ContentType, a.SizeInBytes, SupportTicketAttachmentDownloader.BuildReporterDownloadUrl(ticket.Id, m.Id, a.BlobUrl))).ToArray()
                )
            )
            .ToArray();

        var csat = ticket.Csat is null ? null : new TicketCsatView(ticket.Csat.Score, ticket.Csat.Comment, ticket.Csat.SubmittedAt);

        return new TicketDetailResponse(
            ticket.Id,
            ticket.ShortDisplayId,
            ticket.Subject,
            ticket.Category,
            ticket.Status,
            ticket.CreatedAt,
            ticket.LastActivityAt,
            ticket.ResolvedAt,
            ticket.ClosedAt,
            messages,
            csat
        );
    }
}
