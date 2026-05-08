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
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
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
            logger.LogWarning("No Paystack subscription found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("No active Paystack subscription found.");
        }

        var paystackClient = paystackClientFactory.GetClient();

        var openInvoice = await paystackClient.GetOpenInvoiceAsync(subscription.PaystackSubscriptionId, cancellationToken);
        if (openInvoice is null)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("No pending invoice found for this subscription.");
        }

        var invoiceRetryResult = await paystackClient.RetryOpenInvoicePaymentAsync(subscription.PaystackSubscriptionId, null, cancellationToken);
        if (invoiceRetryResult is null)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("Failed to retry invoice payment.");
        }

        if (invoiceRetryResult is { Paid: false, AccessCode: null, ErrorMessage: not null })
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest(invoiceRetryResult.ErrorMessage);
        }

        if (invoiceRetryResult.Paid)
        {
            events.CollectEvent(new PendingInvoicePaymentRetried(subscription.Id));
        }

        var publicKey = invoiceRetryResult.AccessCode is not null ? paystackClientFactory.GetPublicKey() : null;
        return new RetryPendingInvoicePaymentResponse(invoiceRetryResult.Paid, invoiceRetryResult.AccessCode, invoiceRetryResult.Reference, publicKey, invoiceRetryResult.Amount, invoiceRetryResult.Currency, nameof(PaystackPaymentPurpose.Retry));
    }
}
