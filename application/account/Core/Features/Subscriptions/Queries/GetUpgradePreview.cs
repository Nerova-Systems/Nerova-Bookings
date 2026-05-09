using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetUpgradePreviewQuery(SubscriptionPlan NewPlan) : IRequest<Result<UpgradePreviewResponse>>;

[PublicAPI]
public sealed record UpgradePreviewResponse(UpgradePreviewLineItemResponse[] LineItems, decimal TotalAmount, string Currency);

[PublicAPI]
public sealed record UpgradePreviewLineItemResponse(string Description, decimal Amount, string Currency, bool IsProration, bool IsTax);

public sealed class GetUpgradePreviewHandler(ISubscriptionRepository subscriptionRepository, PaystackClientFactory paystackClientFactory, IExecutionContext executionContext, TimeProvider timeProvider)
    : IRequestHandler<GetUpgradePreviewQuery, Result<UpgradePreviewResponse>>
{
    public async Task<Result<UpgradePreviewResponse>> Handle(GetUpgradePreviewQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<UpgradePreviewResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        if (!query.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result<UpgradePreviewResponse>.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{query.NewPlan}'. Target plan must be higher.");
        }

        if (subscription.PaystackSubscriptionId is null)
        {
            return Result<UpgradePreviewResponse>.BadRequest("No active Paystack authorization found.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var priceCatalog = await paystackClient.GetPriceCatalogAsync(cancellationToken);
        var targetPlanPrice = priceCatalog.SingleOrDefault(p => p.Plan == query.NewPlan);
        if (targetPlanPrice is null)
        {
            return Result<UpgradePreviewResponse>.BadRequest("Could not retrieve upgrade preview.");
        }

        var proratedAmount = SubscriptionBillingCalculator.CalculateProratedUpgradeAmount(
            subscription.CurrentPriceAmount ?? 0m,
            targetPlanPrice.UnitAmount,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            timeProvider.GetUtcNow()
        );
        var currency = targetPlanPrice.Currency.ToUpperInvariant();

        return new UpgradePreviewResponse(
            [
                new UpgradePreviewLineItemResponse($"{query.NewPlan} prorated upgrade", proratedAmount, currency, true, false),
                new UpgradePreviewLineItemResponse("Tax", 0m, currency, false, true)
            ],
            proratedAmount,
            currency
        );
    }
}
