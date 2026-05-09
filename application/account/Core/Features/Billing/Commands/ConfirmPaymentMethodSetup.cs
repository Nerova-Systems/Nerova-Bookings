using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record ConfirmPaymentMethodSetupCommand(string Reference) : ICommand, IRequest<Result<ConfirmPaymentMethodSetupResponse>>;

[PublicAPI]
public sealed record ConfirmPaymentMethodSetupResponse(bool HasPendingRenewalPayment, decimal? PendingRenewalPaymentAmount, string? PendingRenewalPaymentCurrency);

public sealed class ConfirmPaymentMethodSetupHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ILogger<ConfirmPaymentMethodSetupHandler> logger
) : IRequestHandler<ConfirmPaymentMethodSetupCommand, Result<ConfirmPaymentMethodSetupResponse>>
{
    public async Task<Result<ConfirmPaymentMethodSetupResponse>> Handle(ConfirmPaymentMethodSetupCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<ConfirmPaymentMethodSetupResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackCustomerId is null)
        {
            logger.LogWarning("No Paystack customer found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("No Paystack customer found. A subscription must be created first.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var paymentAttempt = await paystackPaymentAttemptRepository.GetByReferenceAsync(command.Reference, cancellationToken);
        if (paymentAttempt is null)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization attempt was not found.");
        }

        if (paymentAttempt.Purpose != PaystackPaymentPurpose.PaymentMethodAuthorization || paymentAttempt.Plan is not null)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Only Paystack payment method authorizations can be confirmed here.");
        }

        if (paymentAttempt.SubscriptionId != subscription.Id || paymentAttempt.PaystackCustomerId != subscription.PaystackCustomerId)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization attempt does not match this subscription.");
        }

        if (paymentAttempt.Status != PaystackPaymentAttemptStatus.Pending)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization attempt has already been processed.");
        }

        var verifiedTransaction = await paystackClient.VerifyPaymentMethodAuthorizationAsync(command.Reference, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (verifiedTransaction?.Paid != true || verifiedTransaction.Authorization is null)
        {
            paymentAttempt.MarkFailed(now, verifiedTransaction?.ErrorMessage ?? "Failed to verify Paystack payment method authorization.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest(verifiedTransaction?.ErrorMessage ?? "Failed to verify Paystack payment method authorization.", true);
        }

        if (!string.Equals(verifiedTransaction.Reference, command.Reference, StringComparison.Ordinal))
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization reference does not match the requested setup reference.");
        }

        if (verifiedTransaction.Purpose != PaystackPaymentPurpose.PaymentMethodAuthorization)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Only Paystack payment method authorizations can be confirmed here.");
        }

        if (verifiedTransaction.CustomerId is not null && verifiedTransaction.CustomerId != subscription.PaystackCustomerId)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization customer does not match this subscription.");
        }

        if (!paymentAttempt.MatchesAmount(verifiedTransaction.Amount, verifiedTransaction.Currency))
        {
            paymentAttempt.MarkFailed(now, "Paystack payment method authorization amount does not match the expected setup amount.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization amount does not match the expected setup amount.", true);
        }

        subscription.SetPaystackAuthorization(verifiedTransaction.Authorization.AuthorizationCode, verifiedTransaction.Authorization.Email, verifiedTransaction.Authorization.Signature, verifiedTransaction.PaymentMethod);

        var refund = await paystackClient.CreateRefundAsync(verifiedTransaction.Reference, verifiedTransaction.Amount, verifiedTransaction.Currency, cancellationToken);
        if (refund is null)
        {
            paymentAttempt.MarkFailed(now, "Failed to refund Paystack payment method authorization charge.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Failed to refund Paystack payment method authorization charge.", true);
        }

        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), refund.Amount, refund.Currency, PaymentTransactionStatus.Refunded, now, null, null, null)
            ]
        );

        subscriptionRepository.Update(subscription);
        paymentAttempt.MarkSucceeded(now);
        paystackPaymentAttemptRepository.Update(paymentAttempt);

        return new ConfirmPaymentMethodSetupResponse(false, null, null);
    }
}
