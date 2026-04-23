using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record CancelSubscriptionCommand(CancellationReason Reason, string? Feedback) : ICommand, IRequest<Result>
{
    public string? Feedback { get; } = Feedback?.Trim();
}

public sealed class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator()
    {
        RuleFor(x => x.Feedback)
            .MaximumLength(500)
            .WithMessage("Feedback must be no longer than 500 characters.")
            .Must(feedback => !feedback!.Contains('<') && !feedback.Contains('>'))
            .WithMessage("Feedback must not contain HTML.")
            .When(x => x.Feedback is not null);
    }
}

public sealed class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<CancelSubscriptionHandler> logger
) : IRequestHandler<CancelSubscriptionCommand, Result>
{
    public async Task<Result> Handle(CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status == SubscriptionStatus.Trial || subscription.Status == SubscriptionStatus.Expired)
        {
            return Result.BadRequest("Cannot cancel a subscription that is not active.");
        }

        if (subscription.Status == SubscriptionStatus.Cancelled)
        {
            return Result.BadRequest("Subscription is already cancelled.");
        }

        if (subscription.PayFastToken is not null)
        {
            var cancelled = await payFastClient.CancelSubscriptionAsync(subscription.PayFastToken, cancellationToken);
            if (!cancelled)
            {
                logger.LogWarning("PayFast cancel API call failed for subscription {SubscriptionId} — proceeding with local cancellation", subscription.Id);
            }
        }

        var now = timeProvider.GetUtcNow();
        var plan = subscription.Plan;
        var price = SubscriptionPlanPricing.GetMonthlyPrice(plan);
        var daysOnPlan = subscription.CurrentPeriodStart.HasValue ? (int)(now - subscription.CurrentPeriodStart.Value).TotalDays : 0;
        var daysUntilExpiry = subscription.CurrentPeriodEnd.HasValue ? (int)(subscription.CurrentPeriodEnd.Value - now).TotalDays : (int?)null;

        subscription.Cancel(command.Reason, command.Feedback, now);

        events.CollectEvent(new SubscriptionCancelled(subscription.Id, plan, command.Reason, daysUntilExpiry, daysOnPlan, price, price, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}
