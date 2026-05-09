using Account.Features.Subscriptions.Domain;

namespace Account.Integrations.Paystack;

public sealed class UnconfiguredPaystackClient(ILogger<UnconfiguredPaystackClient> logger) : IPaystackClient
{
    public Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot create customer for tenant '{TenantName}'", tenantName);
        return Task.FromResult<PaystackCustomerId?>(null);
    }

    public Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot initialize payment for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<CheckoutSessionResult?>(null);
    }

    public Task<SubscriptionSyncResult?> SyncSubscriptionStateAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot sync payment state for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<SubscriptionSyncResult?>(null);
    }

    public Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot charge authorization '{AuthorizationCode}' for upgrade", authorizationCode);
        return Task.FromResult<UpgradeSubscriptionResult?>(null);
    }

    public Task<AuthorizationChargeResult?> ChargeAuthorizationAsync(
        PaystackCustomerId paystackCustomerId,
        PaystackSubscriptionId authorizationCode,
        string email,
        PaystackPaymentPurpose purpose,
        SubscriptionPlan plan,
        decimal amount,
        string currency,
        CancellationToken cancellationToken
    )
    {
        logger.LogWarning("Paystack is not configured. Cannot charge authorization '{AuthorizationCode}' for purpose '{Purpose}'", authorizationCode, purpose);
        return Task.FromResult<AuthorizationChargeResult?>(null);
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

    public Task<VerifiedPaystackTransactionResult?> VerifyTransactionAsync(string reference, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot verify transaction '{Reference}'", reference);
        return Task.FromResult<VerifiedPaystackTransactionResult?>(null);
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
        return Task.FromResult(true);
    }

    public Task<CheckoutSessionResult?> CreatePaymentMethodAuthorizationAsync(PaystackCustomerId paystackCustomerId, string email, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot initialize card authorization for customer '{CustomerId}'", paystackCustomerId);
        return Task.FromResult<CheckoutSessionResult?>(null);
    }

    public Task<VerifiedPaystackTransactionResult?> VerifyPaymentMethodAuthorizationAsync(string reference, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot verify authorization payment '{Reference}'", reference);
        return Task.FromResult<VerifiedPaystackTransactionResult?>(null);
    }

    public Task<RefundResult?> CreateRefundAsync(string transactionReference, decimal amount, string currency, CancellationToken cancellationToken)
    {
        logger.LogWarning("Paystack is not configured. Cannot refund transaction '{Reference}'", transactionReference);
        return Task.FromResult<RefundResult?>(null);
    }

    public Task<bool> SetSubscriptionDefaultPaymentMethodAsync(PaystackSubscriptionId paystackSubscriptionId, string paymentMethodId, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<bool> SetCustomerDefaultPaymentMethodAsync(PaystackCustomerId paystackCustomerId, string paymentMethodId, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<OpenInvoiceResult?> GetOpenInvoiceAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        return Task.FromResult<OpenInvoiceResult?>(null);
    }

    public Task<InvoiceRetryResult?> RetryOpenInvoicePaymentAsync(PaystackSubscriptionId paystackSubscriptionId, string? paymentMethodId, CancellationToken cancellationToken)
    {
        return Task.FromResult<InvoiceRetryResult?>(null);
    }

    public Task<UpgradePreviewResult?> GetUpgradePreviewAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        return Task.FromResult<UpgradePreviewResult?>(null);
    }

    public Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        return Task.FromResult<CheckoutPreviewResult?>(null);
    }

    public Task<SubscribeResult?> CreateSubscriptionWithSavedPaymentMethodAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        return Task.FromResult<SubscribeResult?>(null);
    }

    public Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        return Task.FromResult<PaymentTransaction[]?>(null);
    }
}
