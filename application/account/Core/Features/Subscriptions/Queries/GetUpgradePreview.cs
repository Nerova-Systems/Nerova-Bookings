using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetUpgradePreviewQuery(SubscriptionPlan NewPlan) : IRequest<Result<UpgradePreviewResponse>>;

[PublicAPI]
public sealed record UpgradePreviewResponse(decimal TotalAmount, string Currency, UpgradePreviewLineItemResponse[] LineItems);

[PublicAPI]
public sealed record UpgradePreviewLineItemResponse(string Description, decimal Amount, string Currency, bool IsProration, bool IsTax);

public sealed class GetUpgradePreviewHandler(ISubscriptionRepository subscriptionRepository, IExecutionContext executionContext, TimeProvider timeProvider)
    : IRequestHandler<GetUpgradePreviewQuery, Result<UpgradePreviewResponse>>
{
    public async Task<Result<UpgradePreviewResponse>> Handle(GetUpgradePreviewQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<UpgradePreviewResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status != SubscriptionStatus.Active)
        {
            return Result<UpgradePreviewResponse>.BadRequest("Subscription must be active to preview an upgrade.");
        }

        if (!query.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result<UpgradePreviewResponse>.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{query.NewPlan}'. Target plan must be higher.");
        }

        var now = timeProvider.GetUtcNow();
        var currentPrice = SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan);
        var newPrice = SubscriptionPlanPricing.GetMonthlyPrice(query.NewPlan);

        var daysInPeriod = 30m;
        var daysRemaining = subscription.CurrentPeriodEnd.HasValue ? (decimal)(subscription.CurrentPeriodEnd.Value - now).TotalDays : daysInPeriod;
        daysRemaining = Math.Max(0, Math.Min(daysRemaining, daysInPeriod));

        var proratedCharge = Math.Round((newPrice - currentPrice) * (daysRemaining / daysInPeriod), 2);
        var currency = SubscriptionPlanPricing.Currency;

        var lineItems = new[]
        {
            new UpgradePreviewLineItemResponse($"Unused time on {subscription.Plan} plan", -Math.Round(currentPrice * (daysRemaining / daysInPeriod), 2), currency, true, false),
            new UpgradePreviewLineItemResponse($"Remaining time on {query.NewPlan} plan", Math.Round(newPrice * (daysRemaining / daysInPeriod), 2), currency, true, false)
        };

        return new UpgradePreviewResponse(proratedCharge, currency, lineItems);
    }
}
