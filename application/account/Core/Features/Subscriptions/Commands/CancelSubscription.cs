using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

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
    IExecutionContext executionContext,
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

        return Result.Success();
    }
}
