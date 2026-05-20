using System.Security.Claims;
using Account.Features.Subscriptions.Domain;
using Account.Features.SupportTickets.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.SupportTickets.BackOffice.Queries;

[PublicAPI]
public sealed record GetAllTicketsQuery(
    string? Search = null,
    SupportTicketStatus? Status = null,
    SupportTicketCategory? Category = null,
    SupportTicketAssigneeFilter Assignee = SupportTicketAssigneeFilter.Any,
    string? AssigneeObjectId = null,
    int PageOffset = 0,
    int PageSize = 25
) : IRequest<Result<AllTicketsResponse>>
{
    public string? Search { get; } = Search?.Trim();
}

[PublicAPI]
public sealed record AllTicketsResponse(
    int TotalCount,
    int PageSize,
    int TotalPages,
    int CurrentPageOffset,
    AllTicketsCounts Counts,
    AllTicketsSummary[] Tickets
);

[PublicAPI]
public sealed record AllTicketsCounts(int New, int AwaitingAgent, int AwaitingUser, int AwaitingInternal, int ResolvedLast24Hours);

[PublicAPI]
public sealed record AllTicketsSummary(
    SupportTicketId Id,
    string ShortDisplayId,
    string Subject,
    SupportTicketCategory Category,
    SupportTicketStatus Status,
    TenantId TenantId,
    string TenantName,
    SubscriptionPlan TenantPlan,
    UserId ReporterId,
    string ReporterEmail,
    string? ReporterName,
    string ReporterRoleSnapshot,
    AllTicketsAssignee? Assignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    bool IsUnreadForStaff
);

[PublicAPI]
public sealed record AllTicketsAssignee(string ObjectId, string DisplayName);

public sealed class GetAllTicketsQueryValidator : AbstractValidator<GetAllTicketsQuery>
{
    public GetAllTicketsQueryValidator()
    {
        RuleFor(x => x.Search!).MaximumLength(200).WithMessage("Search must be at most 200 characters.")
            .When(x => x.Search is not null);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
        RuleFor(x => x.PageOffset).GreaterThanOrEqualTo(0).WithMessage("Page offset must be greater than or equal to 0.");
    }
}

public sealed class GetAllTicketsHandler(
    ISupportTicketRepository ticketRepository,
    ITenantRepository tenantRepository,
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider
) : IRequestHandler<GetAllTicketsQuery, Result<AllTicketsResponse>>
{
    public async Task<Result<AllTicketsResponse>> Handle(GetAllTicketsQuery query, CancellationToken cancellationToken)
    {
        var all = await ticketRepository.GetAllUnfilteredAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var resolvedLast24Hours = all.Count(t => t.Status is SupportTicketStatus.Resolved && t.ResolvedAt is not null && t.ResolvedAt.Value >= now.AddHours(-24));
        var counts = new AllTicketsCounts(
            all.Count(t => t.Status is SupportTicketStatus.New),
            all.Count(t => t.Status is SupportTicketStatus.AwaitingAgent),
            all.Count(t => t.Status is SupportTicketStatus.AwaitingUser),
            all.Count(t => t.Status is SupportTicketStatus.AwaitingInternal),
            resolvedLast24Hours
        );

        var meObjectId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var filtered = all.AsEnumerable();

        if (query.Status is { } status) filtered = filtered.Where(t => t.Status == status);
        if (query.Category is { } category) filtered = filtered.Where(t => t.Category == category);
        filtered = query.Assignee switch
        {
            SupportTicketAssigneeFilter.Unassigned => filtered.Where(t => t.Assignee is null),
            SupportTicketAssigneeFilter.Me => filtered.Where(t => t.Assignee is not null && t.Assignee.ObjectId == meObjectId),
            _ when !string.IsNullOrEmpty(query.AssigneeObjectId) => filtered.Where(t => t.Assignee is not null && t.Assignee.ObjectId == query.AssigneeObjectId),
            _ => filtered
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            filtered = filtered.Where(t =>
                t.Subject.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.ShortDisplayId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.ReporterEmailSnapshot.Contains(search, StringComparison.OrdinalIgnoreCase)
            );
        }

        var matches = filtered
            .OrderByDescending(t => t.LastActivityAt)
            .ToArray();
        var totalCount = matches.Length;
        var totalPages = totalCount == 0 ? 0 : (totalCount - 1) / query.PageSize + 1;
        if (query.PageOffset > 0 && query.PageOffset >= totalPages)
        {
            return Result<AllTicketsResponse>.BadRequest($"The page offset '{query.PageOffset}' is greater than the total number of pages.");
        }

        var page = matches.Skip(query.PageOffset * query.PageSize).Take(query.PageSize).ToArray();

        var tenantIds = page.Select(t => t.TenantId).Distinct().ToArray();
        var tenants = (await tenantRepository.GetByIdsUnfilteredAsync(tenantIds, cancellationToken)).ToDictionary(t => t.Id);
        var subscriptions = (await subscriptionRepository.GetByTenantIdsUnfilteredAsync(tenantIds, cancellationToken)).ToDictionary(s => s.TenantId);
        var reporterIds = page.Select(t => t.ReporterId).Distinct().ToArray();
        var reporters = (await userRepository.GetByIdsUnfilteredAsync(reporterIds, cancellationToken)).ToDictionary(u => u.Id);

        var summaries = page.Select(t =>
            {
                var tenant = tenants.GetValueOrDefault(t.TenantId);
                var subscription = subscriptions.GetValueOrDefault(t.TenantId);
                var reporter = reporters.GetValueOrDefault(t.ReporterId);
                var assignee = t.Assignee is null ? null : new AllTicketsAssignee(t.Assignee.ObjectId, t.Assignee.DisplayName);
                return new AllTicketsSummary(
                    t.Id,
                    t.ShortDisplayId,
                    t.Subject,
                    t.Category,
                    t.Status,
                    t.TenantId,
                    tenant?.Name ?? string.Empty,
                    subscription?.Plan ?? tenant?.Plan ?? SubscriptionPlan.Basis,
                    t.ReporterId,
                    t.ReporterEmailSnapshot,
                    reporter is null ? null : $"{reporter.FirstName} {reporter.LastName}".Trim(),
                    t.ReporterRoleSnapshot,
                    assignee,
                    t.CreatedAt,
                    t.LastActivityAt,
                    IsUnreadForStaff(t)
                );
            }
        ).ToArray();

        return new AllTicketsResponse(totalCount, query.PageSize, totalPages, query.PageOffset, counts, summaries);
    }

    // A ticket is "unread" for staff when the most recent message was authored by the user and the
    // ticket has not been picked up yet (AwaitingAgent or New). Approximation that matches what the
    // design surfaces visually as bold rows — there is no per-staff seen state in v1.
    private static bool IsUnreadForStaff(SupportTicket ticket)
    {
        if (ticket.Status is not (SupportTicketStatus.New or SupportTicketStatus.AwaitingAgent)) return false;
        var lastPublic = ticket.Messages.LastOrDefault(m => m.AuthorKind != SupportMessageAuthorKind.Internal);
        return lastPublic is { AuthorKind: SupportMessageAuthorKind.User };
    }
}
