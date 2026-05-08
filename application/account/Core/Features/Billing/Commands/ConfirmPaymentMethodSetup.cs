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
public sealed record ConfirmPaymentMethodSetupResponse(bool HasOpenInvoice, decimal? OpenInvoiceAmount, string? OpenInvoiceCurrency);

public sealed class ConfirmPaymentMethodSetupHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
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
        var verifiedTransaction = await paystackClient.GetSetupIntentPaymentMethodAsync(command.Reference, cancellationToken);
        if (verifiedTransaction?.Paid != true || verifiedTransaction.Authorization is null)
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest(verifiedTransaction?.ErrorMessage ?? "Failed to verify Paystack payment method authorization.");
        }

        OpenInvoiceResult? openInvoice = null;
        subscription.SetPaystackAuthorization(verifiedTransaction.Authorization.AuthorizationCode, verifiedTransaction.Authorization.Email, verifiedTransaction.Authorization.Signature, verifiedTransaction.PaymentMethod);

        if (subscription.HasActivePaystackSubscription())
        {
            var success = await paystackClient.SetSubscriptionDefaultPaymentMethodAsync(verifiedTransaction.Authorization.AuthorizationCode, verifiedTransaction.Authorization.AuthorizationCode.Value, cancellationToken);
            if (!success)
            {
                return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Failed to update subscription payment method.");
            }

            openInvoice = await paystackClient.GetOpenInvoiceAsync(verifiedTransaction.Authorization.AuthorizationCode, cancellationToken);
        }
        else
        {
            var success = await paystackClient.SetCustomerDefaultPaymentMethodAsync(subscription.PaystackCustomerId, verifiedTransaction.Authorization.AuthorizationCode.Value, cancellationToken);
            if (!success)
            {
                return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Failed to update customer payment method.");
            }
        }

        // Subscription is updated and telemetry is collected in ProcessPendingPaystackEvents when Paystack confirms the state change via webhook

        return new ConfirmPaymentMethodSetupResponse(openInvoice is not null, openInvoice?.AmountDue, openInvoice?.Currency);
    }
}
