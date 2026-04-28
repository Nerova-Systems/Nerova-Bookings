using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Jobs;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetCurrentSubscriptionQuery : IRequest<Result<SubscriptionResponse>>;

[PublicAPI]
public sealed record SubscriptionResponse(
    SubscriptionId Id,
    SubscriptionPlan Plan,
    SubscriptionPlan? ScheduledPlan,
    SubscriptionStatus Status,
    DateTimeOffset TrialEndsAt,
    DateTimeOffset? CurrentPeriodEnd,
    DateTimeOffset? NextBillingDate,
    DateTimeOffset? CancelledAt,
    BillingInfo? BillingInfo,
    PaymentMethod? PaymentMethod,
    DateTimeOffset? GracePeriodEndsAt,
    SuspensionReason? SuspensionReason
);

public sealed class GetCurrentSubscriptionHandler(ISubscriptionRepository subscriptionRepository, ITenantRepository tenantRepository)
    : IRequestHandler<GetCurrentSubscriptionQuery, Result<SubscriptionResponse>>
{
    public async Task<Result<SubscriptionResponse>> Handle(GetCurrentSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        DateTimeOffset? gracePeriodEndsAt = subscription.Status == SubscriptionStatus.PastDue && subscription.FirstPaymentFailedAt is not null
            ? subscription.FirstPaymentFailedAt.Value.Add(BillingDunningService.PaymentGracePeriod)
            : null;

        return new SubscriptionResponse(
            subscription.Id,
            subscription.Plan,
            subscription.ScheduledPlan,
            subscription.Status,
            subscription.TrialEndsAt,
            subscription.CurrentPeriodEnd,
            subscription.NextBillingDate,
            subscription.CancelledAt,
            subscription.BillingInfo,
            subscription.PaymentMethod,
            gracePeriodEndsAt,
            tenant?.SuspensionReason
        );
    }
}
