using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ScheduleDowngradeCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result>;

public sealed class ScheduleDowngradeHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ILogger<ScheduleDowngradeHandler> logger
) : IRequestHandler<ScheduleDowngradeCommand, Result>
{
    public async Task<Result> Handle(ScheduleDowngradeCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack subscription found for subscription '{SubscriptionId}'", subscription.Id);
            return Result.BadRequest("No active Paystack subscription found.");
        }

        if (!command.NewPlan.IsDowngradeFrom(subscription.Plan))
        {
            return Result.BadRequest($"Cannot downgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be lower.");
        }

        if (command.NewPlan == SubscriptionPlan.Basis)
        {
            return Result.BadRequest("Cannot downgrade to the Basis plan.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var success = await paystackClient.ScheduleDowngradeAsync(subscription.PaystackSubscriptionId, command.NewPlan, cancellationToken);
        if (!success)
        {
            return Result.BadRequest("Failed to schedule downgrade in Paystack.");
        }

        // Subscription is updated and telemetry is collected in ProcessPendingPaystackEvents when Paystack confirms the state change via webhook

        return Result.Success();
    }
}
