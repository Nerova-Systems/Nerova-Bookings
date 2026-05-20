using Account.Features.SupportTickets.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.SupportTickets.Queries;

[PublicAPI]
public sealed record GetMyTicketsQuery : IRequest<Result<MyTicketsResponse>>;

[PublicAPI]
public sealed record MyTicketsResponse(MyTicketSummary[] Active, MyTicketSummary[] Closed, int AwaitingUserCount);

[PublicAPI]
public sealed record MyTicketSummary(
    SupportTicketId Id,
    string ShortDisplayId,
    string Subject,
    SupportTicketCategory Category,
    SupportTicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    int MessagesCount,
    int AttachmentsCount
);

public sealed class GetMyTicketsHandler(ISupportTicketRepository ticketRepository, IExecutionContext executionContext)
    : IRequestHandler<GetMyTicketsQuery, Result<MyTicketsResponse>>
{
    public async Task<Result<MyTicketsResponse>> Handle(GetMyTicketsQuery query, CancellationToken cancellationToken)
    {
        var reporterId = executionContext.UserInfo.Id!;
        var tickets = await ticketRepository.GetTenantTicketsAsync(cancellationToken);
        var owned = tickets.Where(t => t.ReporterId == reporterId).ToArray();

        var active = owned
            .Where(t => t.Status is not SupportTicketStatus.Closed)
            .OrderByDescending(t => t.LastActivityAt)
            .Select(ToSummary)
            .ToArray();
        var closed = owned
            .Where(t => t.Status is SupportTicketStatus.Closed)
            .OrderByDescending(t => t.LastActivityAt)
            .Select(ToSummary)
            .ToArray();
        var awaitingUser = owned.Count(t => t.Status is SupportTicketStatus.AwaitingUser);

        return new MyTicketsResponse(active, closed, awaitingUser);
    }

    private static MyTicketSummary ToSummary(SupportTicket ticket)
    {
        var publicMessages = ticket.Messages.Where(m => m.AuthorKind != SupportMessageAuthorKind.Internal).ToArray();
        return new MyTicketSummary(
            ticket.Id,
            ticket.ShortDisplayId,
            ticket.Subject,
            ticket.Category,
            ticket.Status,
            ticket.CreatedAt,
            ticket.LastActivityAt,
            publicMessages.Length,
            publicMessages.Sum(m => m.Attachments.Length)
        );
    }
}
