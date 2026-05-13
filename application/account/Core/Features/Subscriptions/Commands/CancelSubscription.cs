using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
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
            .WithMessage("Feedback must be no longer than 500 characters.")
            .When(x => x.Feedback is not null);
    }
}

public sealed class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IBillingEventRepository billingEventRepository,
    IExecutionContext executionContext,
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

        if (subscription.Plan == SubscriptionPlan.Basis)
        {
            return Result.BadRequest("Cannot cancel a Basis subscription.");
        }

        if (subscription.PaystackAuthorizationCode is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result.BadRequest("No active Paystack authorization found.");
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return Result.BadRequest("Subscription is already scheduled for cancellation.");
        }

        subscription.SetCancellation(true, command.Reason, command.Feedback);
        subscriptionRepository.Update(subscription);

        var now = timeProvider.GetUtcNow();
        var priceAmount = subscription.CurrentPriceAmount ?? 0m;
        var currency = subscription.CurrentPriceCurrency;
        var billingEvent = BillingEvent.Create(
            subscription.TenantId,
            subscription.Id,
            $"paystack:{subscription.Id}:cancel:{now.ToUnixTimeMilliseconds()}",
            BillingEventType.SubscriptionCancelled,
            now,
            0m,
            subscription.Plan,
            subscription.Plan,
            priceAmount,
            0m,
            -priceAmount,
            currency,
            command.Reason
        );
        await billingEventRepository.AddAsync(billingEvent, cancellationToken);

        int? daysUntilExpiry = subscription.CurrentPeriodEnd is null ? null : Math.Max(0, (subscription.CurrentPeriodEnd.Value - now).Days);
        var daysOnCurrentPlan = subscription.CurrentPeriodStart is null ? 0 : Math.Max(0, (now - subscription.CurrentPeriodStart.Value).Days);
        events.CollectEvent(new SubscriptionCancelled(subscription.Id, subscription.Plan, command.Reason, daysUntilExpiry, daysOnCurrentPlan, priceAmount, -priceAmount, currency ?? "unknown"));

        return Result.Success();
    }
}
