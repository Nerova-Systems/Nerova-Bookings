using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.BackOffice.Dashboard.Queries;

[PublicAPI]
public sealed record GetDashboardRecentPaystackEventsQuery(int Limit = 6)
    : IRequest<Result<BackOfficeDashboardRecentPaystackEventsResponse>>;

[PublicAPI]
public sealed record BackOfficeDashboardRecentPaystackEventsResponse(BackOfficeDashboardPaystackEvent[] Events);

[PublicAPI]
public sealed record BackOfficeDashboardPaystackEvent(
    BillingEventId Id,
    TenantId TenantId,
    string TenantName,
    string? TenantLogoUrl,
    BillingEventType Type,
    SubscriptionPlan? FromPlan,
    SubscriptionPlan? ToPlan,
    decimal? AmountDelta,
    string? Currency,
    DateTimeOffset OccurredAt
);

public sealed class GetDashboardRecentPaystackEventsQueryValidator : AbstractValidator<GetDashboardRecentPaystackEventsQuery>
{
    public GetDashboardRecentPaystackEventsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");
    }
}

public sealed class GetDashboardRecentPaystackEventsHandler(IBillingEventRepository billingEventRepository, ITenantRepository tenantRepository)
    : IRequestHandler<GetDashboardRecentPaystackEventsQuery, Result<BackOfficeDashboardRecentPaystackEventsResponse>>
{
    public async Task<Result<BackOfficeDashboardRecentPaystackEventsResponse>> Handle(GetDashboardRecentPaystackEventsQuery query, CancellationToken cancellationToken)
    {
        var billingEvents = await billingEventRepository.GetRecentUnfilteredAsync(query.Limit, cancellationToken);
        if (billingEvents.Length == 0) return new BackOfficeDashboardRecentPaystackEventsResponse([]);

        var tenantIds = billingEvents.Select(e => e.TenantId).Distinct().ToArray();
        var tenants = await tenantRepository.GetByIdsUnfilteredAsync(tenantIds, cancellationToken);
        var tenantsById = tenants.ToDictionary(t => t.Id);

        var events = billingEvents
            .Where(e => tenantsById.ContainsKey(e.TenantId))
            .Select(e =>
                {
                    var tenant = tenantsById[e.TenantId];
                    return new BackOfficeDashboardPaystackEvent(
                        e.Id,
                        tenant.Id,
                        tenant.Name,
                        tenant.Logo.Url,
                        e.EventType,
                        e.FromPlan,
                        e.ToPlan,
                        e.AmountDelta,
                        e.Currency,
                        e.OccurredAt
                    );
                }
            )
            .ToArray();

        return new BackOfficeDashboardRecentPaystackEventsResponse(events);
    }
}
