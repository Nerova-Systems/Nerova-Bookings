using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record RetryPendingInvoicePaymentCommand : ICommand, IRequest<Result<RetryPendingInvoicePaymentResponse>>;

[PublicAPI]
public sealed record RetryPendingInvoicePaymentResponse(
    bool Paid,
    string? AccessCode,
    string? Reference,
    string? PublicKey,
    decimal? Amount,
    string? Currency,
    string OperationPurpose
);

public sealed class RetryPendingInvoicePaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<RetryPendingInvoicePaymentHandler> logger
) : IRequestHandler<RetryPendingInvoicePaymentCommand, Result<RetryPendingInvoicePaymentResponse>>
{
    public async Task<Result<RetryPendingInvoicePaymentResponse>> Handle(RetryPendingInvoicePaymentCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<RetryPendingInvoicePaymentResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("No active Paystack authorization found.");
        }

        if (subscription.FirstPaymentFailedAt is null || subscription.CurrentPriceAmount is null || subscription.CurrentPriceCurrency is null)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("No pending renewal payment found for this subscription.");
        }

        var billingEmail = subscription.PaystackAuthorizationEmail ?? subscription.BillingInfo?.Email;
        if (billingEmail is null)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("Billing information must include an email before retrying payment.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var charge = await paystackClient.ChargeAuthorizationAsync(
            subscription.PaystackCustomerId!,
            subscription.PaystackSubscriptionId,
            billingEmail,
            PaystackPaymentPurpose.Retry,
            subscription.Plan,
            subscription.CurrentPriceAmount.Value,
            subscription.CurrentPriceCurrency,
            cancellationToken
        );
        if (charge is null)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("Failed to retry renewal payment.");
        }

        var paymentAttempt = PaystackPaymentAttempt.Create(
            subscription.TenantId,
            subscription.Id,
            charge.Reference,
            subscription.PaystackCustomerId!,
            subscription.PaystackSubscriptionId,
            PaystackPaymentPurpose.Retry,
            subscription.Plan,
            charge.Amount,
            charge.Currency
        );

        var now = timeProvider.GetUtcNow();
        if (!charge.Paid)
        {
            paymentAttempt.MarkFailed(now, charge.ErrorMessage ?? "Paystack could not charge the saved payment method.");
            await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest(charge.ErrorMessage ?? "Paystack could not charge the saved payment method.", true);
        }

        var nextBillingAt = now.AddMonths(1);
        paymentAttempt.MarkSucceeded(now);
        subscription.ClearPaymentFailure();
        subscription.StartBillingPeriod(subscription.Plan, charge.Amount, charge.Currency, now, nextBillingAt, charge.PaymentMethod);
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), charge.Amount, charge.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );
        subscriptionRepository.Update(subscription);
        await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
        events.CollectEvent(new RenewalPaymentRetried(subscription.Id));

        return new RetryPendingInvoicePaymentResponse(true, null, charge.Reference, null, charge.Amount, charge.Currency, nameof(PaystackPaymentPurpose.Retry));
    }
}
