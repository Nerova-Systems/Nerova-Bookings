using Account.Features.Subscriptions.Domain;
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
    DateTimeOffset? CancelledAt
);

public sealed class GetCurrentSubscriptionHandler(ISubscriptionRepository subscriptionRepository)
    : IRequestHandler<GetCurrentSubscriptionQuery, Result<SubscriptionResponse>>
{
    public async Task<Result<SubscriptionResponse>> Handle(GetCurrentSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        return new SubscriptionResponse(
            subscription.Id,
            subscription.Plan,
            subscription.ScheduledPlan,
            subscription.Status,
            subscription.TrialEndsAt,
            subscription.CurrentPeriodEnd,
            subscription.NextBillingDate,
            subscription.CancelledAt
        );
    }
}
