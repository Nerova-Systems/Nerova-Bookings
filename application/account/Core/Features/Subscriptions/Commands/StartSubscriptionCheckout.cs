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
public sealed record StartSubscriptionCheckoutResponse(
    string? AccessCode,
    string? Reference,
    string? PublicKey,
    decimal? Amount,
    string? Currency,
    string OperationPurpose,
    bool UsedExistingPaymentMethod
);

public sealed class StartSubscriptionCheckoutValidator : AbstractValidator<StartSubscriptionCheckoutCommand>
{
    public StartSubscriptionCheckoutValidator()
    {
        RuleFor(x => x.Plan).NotEqual(SubscriptionPlan.Basis).WithMessage("Cannot subscribe to the Basis plan.");
    }
}

public sealed class StartSubscriptionCheckoutHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    ILogger<StartSubscriptionCheckoutHandler> logger
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

        var publicKey = paystackClientFactory.GetPublicKey();
        if (publicKey is null)
        {
            logger.LogWarning("Paystack public key is not configured");
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("Paystack is not configured for checkout.");
        }

        var paystackClient = paystackClientFactory.GetClient();

        if (subscription.PaymentMethod is not null)
        {
            if (subscription.HasActivePaystackSubscription())
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("A subscription already exists. Please complete any pending payment or use upgrade instead.");
            }

            if (subscription.BillingInfo is null)
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("Billing information must be saved before subscribing.");
            }

            if (subscription.PaystackSubscriptionId is null || subscription.BillingInfo.Email is null)
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("A reusable card authorization is required before subscribing with a saved payment method.");
            }

            var subscribeResult = await paystackClient.CreateSubscriptionWithSavedPaymentMethodAsync(subscription.PaystackCustomerId, subscription.PaystackSubscriptionId, subscription.BillingInfo.Email, command.Plan, cancellationToken);
            if (subscribeResult is null)
            {
                return Result<StartSubscriptionCheckoutResponse>.BadRequest("Failed to charge saved payment method.");
            }

            await paystackPaymentAttemptRepository.AddAsync(
                PaystackPaymentAttempt.Create(
                    subscription.TenantId,
                    subscription.Id,
                    subscribeResult.Reference,
                    subscription.PaystackCustomerId,
                    subscription.PaystackSubscriptionId,
                    PaystackPaymentPurpose.Subscribe,
                    command.Plan,
                    subscribeResult.Amount,
                    subscribeResult.Currency
                ),
                cancellationToken
            );

            events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, command.Plan, true));

            return new StartSubscriptionCheckoutResponse(null, subscribeResult.Reference, null, subscribeResult.Amount, subscribeResult.Currency, nameof(PaystackPaymentPurpose.Subscribe), true);
        }

        if (subscription.HasActivePaystackSubscription())
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("An active subscription already exists. Cannot create a new checkout session.");
        }

        if (subscription.BillingInfo?.Email is null)
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("Billing information must include an email before checkout.");
        }

        var result = await paystackClient.CreateCheckoutSessionAsync(subscription.PaystackCustomerId!, subscription.BillingInfo.Email, command.Plan, PaystackPaymentPurpose.Subscribe, cancellationToken);
        if (result is null)
        {
            return Result<StartSubscriptionCheckoutResponse>.BadRequest("Failed to initialize Paystack checkout.");
        }

        await paystackPaymentAttemptRepository.AddAsync(
            PaystackPaymentAttempt.Create(
                subscription.TenantId,
                subscription.Id,
                result.Reference,
                subscription.PaystackCustomerId,
                null,
                PaystackPaymentPurpose.Subscribe,
                command.Plan,
                result.Amount,
                result.Currency
            ),
            cancellationToken
        );

        events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, command.Plan, false));

        return new StartSubscriptionCheckoutResponse(result.AccessCode, result.Reference, publicKey, result.Amount, result.Currency, result.Purpose.ToString(), false);
    }
}
