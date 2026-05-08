using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
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
    IConfiguration configuration,
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

        if (!AmountsMatch(verifiedTransaction.Amount, GetExpectedAuthorizationAmount()) || !CurrenciesMatch(verifiedTransaction.Currency, GetExpectedAuthorizationCurrency()))
        {
            return Result<ConfirmPaymentMethodSetupResponse>.BadRequest("Paystack payment method authorization amount does not match the expected setup amount.");
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

    private decimal GetExpectedAuthorizationAmount()
    {
        var amountSubunit = configuration.GetValue<long?>("Paystack:CardAuthorizationAmountSubunit") ?? 100;
        return decimal.Round(amountSubunit / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private string GetExpectedAuthorizationCurrency()
    {
        return (configuration["Paystack:CardAuthorizationCurrency"] ?? "USD").ToUpperInvariant();
    }

    private static bool AmountsMatch(decimal actual, decimal expected)
    {
        return decimal.Round(actual, 2, MidpointRounding.AwayFromZero) == decimal.Round(expected, 2, MidpointRounding.AwayFromZero);
    }

    private static bool CurrenciesMatch(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
