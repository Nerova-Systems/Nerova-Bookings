using Account.Features.Subscriptions.Domain;
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
    // TODO: Replace with PayFast plan catalog in pf-03
    public Task<Result<PricingCatalogResponse>> Handle(GetPricingCatalogQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<PricingCatalogResponse>.Success(new PricingCatalogResponse([])));
    }
}
