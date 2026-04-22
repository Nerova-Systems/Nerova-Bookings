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
            .WithMessage("Feedback must not contain HTML.")
            .When(x => x.Feedback is not null);
    }
}

public sealed class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext
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

        // TODO: Implement PayFast cancellation in pf-03

        return Result.Success();
    }
}
