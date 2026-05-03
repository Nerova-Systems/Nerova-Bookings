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
    bool HasPaystackCustomer,
    bool HasPaystackSubscription,
    decimal? CurrentPriceAmount,
    string? CurrentPriceCurrency,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    bool IsPaymentFailed,
    PaymentMethod? PaymentMethod,
    BillingInfo? BillingInfo,
    bool HasPendingPaystackEvents
);

public sealed class GetCurrentSubscriptionHandler(ISubscriptionRepository subscriptionRepository, IPaystackEventRepository paystackEventRepository)
    : IRequestHandler<GetCurrentSubscriptionQuery, Result<SubscriptionResponse>>
{
    public async Task<Result<SubscriptionResponse>> Handle(GetCurrentSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        var hasPendingPaystackEvents = subscription.PaystackCustomerId is not null
                                     && await paystackEventRepository.HasPendingByPaystackCustomerIdAsync(subscription.PaystackCustomerId, cancellationToken);

        return new SubscriptionResponse(
            subscription.Id,
            subscription.Plan,
            null,
            subscription.PaystackCustomerId is not null,
            subscription.PaystackSubscriptionId is not null,
            subscription.CurrentPriceAmount,
            subscription.CurrentPriceCurrency,
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            subscription.FirstPaymentFailedAt is not null,
            subscription.PaymentMethod,
            subscription.BillingInfo,
            hasPendingPaystackEvents
        );
    }
}
