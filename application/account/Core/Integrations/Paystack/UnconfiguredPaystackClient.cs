using Account.Features.Subscriptions.Domain;

namespace Account.Integrations.Paystack;

public sealed class UnconfiguredPaystackClient(ILogger<UnconfiguredPaystackClient> logger) : IPaystackClient
{
    public Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot create customer for tenant '{TenantName}'", tenantName);
        return Task.FromResult<PaystackCustomerId?>(null);
    }

    public Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, string? locale, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot create checkout session for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<CheckoutSessionResult?>(null);
    }

    public Task<SubscriptionSyncResult?> SyncSubscriptionStateAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot sync subscription state for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<SubscriptionSyncResult?>(null);
    }

    public Task<PaystackSubscriptionId?> GetCheckoutSessionSubscriptionIdAsync(string reference, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot verify checkout reference '{Reference}'", reference);
        return Task.FromResult<PaystackSubscriptionId?>(null);
    }

    public Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot upgrade subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult<UpgradeSubscriptionResult?>(null);
    }

    public Task<bool> ScheduleDowngradeAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot schedule downgrade for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult(false);
    }

    public Task<bool> CancelScheduledDowngradeAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot cancel scheduled downgrade for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult(false);
    }

    public Task<bool> CancelSubscriptionAtPeriodEndAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationReason reason, string? feedback, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot cancel subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult(false);
    }

    public Task<bool> ReactivateSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot reactivate subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult(false);
    }

    public Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot get pricing catalog");
        return Task.FromResult<PriceCatalogItem[]>([]);
    }

    public PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader)
    {
        logger.LogWarning("Paystack is not configured. Cannot verify webhook signature");
        return null;
    }

    public Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot get customer billing info for '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<CustomerBillingResult?>(null);
    }

    public Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot update billing info for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult(false);
    }

    public Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot sync tax ID for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult(false);
    }

    public Task<string?> CreatePaymentMethodUpdateLinkAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot create payment method update link for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult<string?>(null);
    }

    public Task<PaymentMethod?> GetPaymentMethodFromTransactionAsync(string reference, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot get payment method from transaction '{Reference}'", reference);
        return Task.FromResult<PaymentMethod?>(null);
    }

    public Task<bool> SetSubscriptionDefaultPaymentMethodAsync(PaystackSubscriptionId paystackSubscriptionId, string paymentMethodId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot update payment method for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult(false);
    }

    public Task<bool> SetCustomerDefaultPaymentMethodAsync(PaystackCustomerId paystackCustomerId, string paymentMethodId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot update payment method for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult(false);
    }

    public Task<OpenInvoiceResult?> GetOpenInvoiceAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot check open invoices for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult<OpenInvoiceResult?>(null);
    }

    public Task<InvoiceRetryResult?> RetryOpenInvoicePaymentAsync(PaystackSubscriptionId paystackSubscriptionId, string? paymentMethodId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot retry invoice payment for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult<InvoiceRetryResult?>(null);
    }

    public Task<UpgradePreviewResult?> GetUpgradePreviewAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot get upgrade preview for subscription '{SubscriptionId}'", paystackSubscriptionId);
        return Task.FromResult<UpgradePreviewResult?>(null);
    }

    public Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot get checkout preview for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<CheckoutPreviewResult?>(null);
    }

    public Task<SubscribeResult?> CreateSubscriptionWithSavedPaymentMethodAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot create subscription for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<SubscribeResult?>(null);
    }

    public Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot sync payment transactions for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<PaymentTransaction[]?>(null);
    }
}
