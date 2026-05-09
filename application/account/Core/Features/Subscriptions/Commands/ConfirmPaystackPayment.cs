using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ConfirmPaystackPaymentCommand(string Reference, SubscriptionPlan Plan, PaystackPaymentPurpose Purpose)
    : ICommand, IRequest<Result<ConfirmPaystackPaymentResponse>>;

[PublicAPI]
public sealed record ConfirmPaystackPaymentResponse(bool Paid);

public sealed class ConfirmPaystackPaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    ITenantRepository tenantRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<ConfirmPaystackPaymentCommand, Result<ConfirmPaystackPaymentResponse>>
{
    public async Task<Result<ConfirmPaystackPaymentResponse>> Handle(ConfirmPaystackPaymentCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<ConfirmPaystackPaymentResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        if (command.Plan == SubscriptionPlan.Basis)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Cannot confirm payment for the Basis plan.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        if (subscription.PaystackCustomerId is null)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Billing information must be saved before payment confirmation.");
        }

        if (command.Purpose is not PaystackPaymentPurpose.Subscribe and not PaystackPaymentPurpose.Upgrade)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Only subscription Paystack payments can be confirmed here.");
        }

        var paymentAttempt = await paystackPaymentAttemptRepository.GetByReferenceAsync(command.Reference, cancellationToken);
        if (paymentAttempt is null)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment attempt was not found.");
        }

        if (paymentAttempt.SubscriptionId != subscription.Id
            || paymentAttempt.PaystackCustomerId != subscription.PaystackCustomerId
            || !paymentAttempt.Matches(command.Purpose, command.Plan))
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment attempt does not match the requested confirmation.");
        }

        if (paymentAttempt.Status != PaystackPaymentAttemptStatus.Pending)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment attempt has already been processed.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var verified = await paystackClient.VerifyTransactionAsync(command.Reference, command.Purpose, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (verified?.Paid != true)
        {
            paymentAttempt.MarkFailed(now, verified?.ErrorMessage ?? "Paystack payment could not be verified.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmPaystackPaymentResponse>.BadRequest(verified?.ErrorMessage ?? "Paystack payment could not be verified.");
        }

        if (!string.Equals(verified.Reference, command.Reference, StringComparison.Ordinal))
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment reference does not match the requested confirmation reference.");
        }

        if (verified.Purpose != command.Purpose)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment purpose does not match the requested confirmation purpose.");
        }

        if (verified.CustomerId is not null && verified.CustomerId != subscription.PaystackCustomerId)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment customer does not match this subscription.");
        }

        if (verified.Authorization is null)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment did not return a reusable card authorization.");
        }

        if (!paymentAttempt.MatchesAmount(verified.Amount, verified.Currency))
        {
            paymentAttempt.MarkFailed(now, "Paystack payment amount does not match the expected subscription amount.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment amount does not match the expected subscription amount.");
        }

        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount;
        subscription.SetPaystackAuthorization(verified.Authorization.AuthorizationCode, verified.Authorization.Email, verified.Authorization.Signature, verified.PaymentMethod);
        var nextBillingAt = now.AddMonths(1);
        subscription.SetPaystackSubscription(verified.Authorization.AuthorizationCode, command.Plan, verified.Amount, verified.Currency, now, nextBillingAt, nextBillingAt, verified.PaymentMethod);
        subscription.ClearPaymentFailure();
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), verified.Amount, verified.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        if (tenant is not null)
        {
            tenant.UpdatePlan(command.Plan);
            tenant.Activate();
            tenantRepository.Update(tenant);
        }

        subscriptionRepository.Update(subscription);
        paymentAttempt.MarkSucceeded(now);
        paystackPaymentAttemptRepository.Update(paymentAttempt);

        if (previousPlan == SubscriptionPlan.Basis)
        {
            events.CollectEvent(new SubscriptionCreated(subscription.Id, command.Plan, verified.Amount, verified.Amount, verified.Currency));
        }
        else if (command.Plan.IsUpgradeFrom(previousPlan))
        {
            events.CollectEvent(new SubscriptionUpgraded(subscription.Id, previousPlan, command.Plan, 0, previousPriceAmount ?? 0m, verified.Amount, verified.Amount - (previousPriceAmount ?? 0m), verified.Currency));
        }

        return new ConfirmPaystackPaymentResponse(true);
    }
}
