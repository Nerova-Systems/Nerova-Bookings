using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record StartSubscriptionCheckoutCommand(SubscriptionPlan Plan)
    : ICommand, IRequest<Result<StartSubscriptionCheckoutResponse>>;

[PublicAPI]
public sealed record StartSubscriptionCheckoutResponse(string? AuthorizationUrl, string? Reference, bool UsedExistingPaymentMethod);

public sealed class StartSubscriptionCheckoutValidator : AbstractValidator<StartSubscriptionCheckoutCommand>
{
    public StartSubscriptionCheckoutValidator()
    {
        RuleFor(x => x.Plan).NotEqual(SubscriptionPlan.Basis).WithMessage("Cannot subscribe to the Basis plan.");
    }
}

public sealed class StartSubscriptionCheckoutHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<StartSubscriptionCheckoutCommand, Result<StartSubscriptionCheckoutResponse>>
{
    public async Task<Result<StartSubscriptionCheckoutResponse>> Handle(StartSubscriptionCheckoutCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<StartSubscriptionCheckoutResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackCustomerId is null)
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("Billing information must be saved before checkout.");
        }

        var userEmail = executionContext.UserInfo.Email;
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("User email is required to create a Paystack checkout session.");
        }

        var paystackClient = paystackClientFactory.GetClient();

        if (subscription.PaymentMethod is not null)
        {
            if (subscription.PaystackSubscriptionId is not null)
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("A subscription already exists. Please complete any pending payment or use upgrade instead.");
            }

            if (subscription.BillingInfo is null)
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("Billing information must be saved before subscribing.");
            }

            var subscribeResult = await paystackClient.CreateSubscriptionWithSavedPaymentMethodAsync(subscription.PaystackCustomerId, command.Plan, cancellationToken);
            if (subscribeResult is null)
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("Failed to create subscription in Paystack.");
            }

            events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, command.Plan, true));

            return new StartSubscriptionCheckoutResponse(subscribeResult.AuthorizationUrl, subscribeResult.Reference, true);
        }

        if (subscription.HasActivePaystackSubscription())
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("An active subscription already exists. Cannot create a new checkout session.");
        }

        var result = await paystackClient.CreateCheckoutSessionAsync(subscription.PaystackCustomerId!, userEmail, command.Plan, executionContext.UserInfo.Locale, cancellationToken);
        if (result is null)
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("Failed to create checkout session.");
        }

        events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, command.Plan, false));

        return new StartSubscriptionCheckoutResponse(result.AuthorizationUrl, result.Reference, false);
    }
}
