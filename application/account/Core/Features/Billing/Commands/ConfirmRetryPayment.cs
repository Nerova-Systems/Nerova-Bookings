using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record ConfirmRetryPaymentCommand(string Reference) : ICommand, IRequest<Result<ConfirmRetryPaymentResponse>>;

[PublicAPI]
public sealed record ConfirmRetryPaymentResponse(bool Paid);

public sealed class ConfirmRetryPaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<ConfirmRetryPaymentCommand, Result<ConfirmRetryPaymentResponse>>
{
    public async Task<Result<ConfirmRetryPaymentResponse>> Handle(ConfirmRetryPaymentCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<ConfirmRetryPaymentResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        if (subscription.PaystackCustomerId is null)
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("No Paystack customer found.");
        }

        var paymentAttempt = await paystackPaymentAttemptRepository.GetByReferenceAsync(command.Reference, cancellationToken);
        if (paymentAttempt is null)
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Paystack retry payment attempt was not found.");
        }

        if (paymentAttempt.SubscriptionId != subscription.Id
            || paymentAttempt.PaystackCustomerId != subscription.PaystackCustomerId
            || !paymentAttempt.Matches(PaystackPaymentPurpose.Retry, subscription.Plan))
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Paystack retry payment attempt does not match this subscription.");
        }

        if (paymentAttempt.Status != PaystackPaymentAttemptStatus.Pending)
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Paystack retry payment attempt has already been processed.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var verified = await paystackClient.VerifyTransactionAsync(command.Reference, PaystackPaymentPurpose.Retry, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (verified?.Paid != true)
        {
            paymentAttempt.MarkFailed(now, verified?.ErrorMessage ?? "Paystack retry payment could not be verified.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmRetryPaymentResponse>.BadRequest(verified?.ErrorMessage ?? "Paystack retry payment could not be verified.", true);
        }

        if (!string.Equals(verified.Reference, command.Reference, StringComparison.Ordinal))
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Paystack retry payment reference does not match the requested confirmation reference.");
        }

        if (verified.Purpose != PaystackPaymentPurpose.Retry)
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Only Paystack retry payments can be confirmed here.");
        }

        if (verified.CustomerId is not null && verified.CustomerId != subscription.PaystackCustomerId)
        {
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Paystack retry payment customer does not match this subscription.");
        }

        if (!paymentAttempt.MatchesAmount(verified.Amount, verified.Currency))
        {
            paymentAttempt.MarkFailed(now, "Paystack retry payment amount does not match the expected renewal payment amount.");
            paystackPaymentAttemptRepository.Update(paymentAttempt);
            return Result<ConfirmRetryPaymentResponse>.BadRequest("Paystack retry payment amount does not match the expected renewal payment amount.", true);
        }

        subscription.ClearPaymentFailure();
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), verified.Amount, verified.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );
        subscriptionRepository.Update(subscription);

        paymentAttempt.MarkSucceeded(now);
        paystackPaymentAttemptRepository.Update(paymentAttempt);
        events.CollectEvent(new RenewalPaymentRetried(subscription.Id));

        return new ConfirmRetryPaymentResponse(true);
    }
}
