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

public sealed class GetUpgradePreviewHandler(ISubscriptionRepository subscriptionRepository, IExecutionContext executionContext)
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

        // TODO: Implement PayFast upgrade preview in pf-03
        return Result<UpgradePreviewResponse>.BadRequest("Upgrade preview not yet available.");
    }
}
