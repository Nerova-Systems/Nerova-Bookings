using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetPricingCatalogQuery : IRequest<Result<PricingCatalogResponse>>;

[PublicAPI]
public sealed record PricingCatalogResponse(PlanPriceItem[] Plans);

[PublicAPI]
public sealed record PlanPriceItem(
    SubscriptionPlan Plan,
    decimal UnitAmount,
    string Currency,
    string Interval,
    int IntervalCount,
    bool TaxInclusive
);

public sealed class GetPricingCatalogHandler : IRequestHandler<GetPricingCatalogQuery, Result<PricingCatalogResponse>>
{
    public Task<Result<PricingCatalogResponse>> Handle(GetPricingCatalogQuery query, CancellationToken cancellationToken)
    {
        var plans = new[]
        {
            SubscriptionPlan.Starter,
            SubscriptionPlan.Standard,
            SubscriptionPlan.Premium
        }.Select(plan => new PlanPriceItem(
            plan,
            SubscriptionPlanPricing.GetMonthlyPrice(plan),
            SubscriptionPlanPricing.Currency,
            "month",
            1,
            true
        )).ToArray();

        return Task.FromResult(Result<PricingCatalogResponse>.Success(new PricingCatalogResponse(plans)));
    }
}
