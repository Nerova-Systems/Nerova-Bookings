using Account.Features.Subscriptions.Domain;

namespace Account.Integrations.Paystack;

public interface IPaystackClient
{
    Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken);

    Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, string? locale, CancellationToken cancellationToken);

    Task<SubscriptionSyncResult?> SyncSubscriptionStateAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    Task<PaystackSubscriptionId?> GetCheckoutSessionSubscriptionIdAsync(string reference, CancellationToken cancellationToken);

    Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken);

    Task<bool> CancelSubscriptionAtPeriodEndAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationReason reason, string? feedback, CancellationToken cancellationToken);

    Task<bool> ReactivateSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken);

    Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken);

    PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader);

    Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken);

    Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken);

    Task<string?> CreatePaymentMethodUpdateLinkAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken);

    Task<PaymentMethod?> GetPaymentMethodFromTransactionAsync(string reference, CancellationToken cancellationToken);

    Task<bool> SetSubscriptionDefaultPaymentMethodAsync(PaystackSubscriptionId paystackSubscriptionId, string paymentMethodId, CancellationToken cancellationToken);

    Task<bool> SetCustomerDefaultPaymentMethodAsync(PaystackCustomerId paystackCustomerId, string paymentMethodId, CancellationToken cancellationToken);

    Task<OpenInvoiceResult?> GetOpenInvoiceAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken);

    Task<InvoiceRetryResult?> RetryOpenInvoicePaymentAsync(PaystackSubscriptionId paystackSubscriptionId, string? paymentMethodId, CancellationToken cancellationToken);

    Task<UpgradePreviewResult?> GetUpgradePreviewAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken);

    Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken);

    Task<SubscribeResult?> CreateSubscriptionWithSavedPaymentMethodAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken);

    Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);
}

public sealed record PaystackWebhookEventResult(
    string EventId,
    string EventType,
    PaystackCustomerId? CustomerId
);

public sealed record CheckoutSessionResult(string Reference, string AuthorizationUrl);

public sealed record SubscriptionSyncResult(
    SubscriptionPlan Plan,
    SubscriptionPlan? ScheduledPlan,
    PaystackSubscriptionId? PaystackSubscriptionId,
    decimal? CurrentPriceAmount,
    string? CurrentPriceCurrency,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    CancellationReason? CancellationReason,
    string? CancellationFeedback,
    PaymentTransaction[]? PaymentTransactions,
    PaymentMethod? PaymentMethod,
    string? SubscriptionStatus
);

public sealed record CustomerBillingResult(BillingInfo? BillingInfo, bool IsCustomerDeleted, PaymentMethod? PaymentMethod = null);

public sealed record OpenInvoiceResult(decimal AmountDue, string Currency);

public sealed record InvoiceRetryResult(bool Paid, string? AuthorizationUrl, string? Reference, string? ErrorMessage = null);

public sealed record UpgradeSubscriptionResult(string? AuthorizationUrl, string? Reference, string? ErrorMessage = null);

public sealed record SubscribeResult(string? AuthorizationUrl, string? Reference);

public sealed record UpgradePreviewResult(decimal TotalAmount, string Currency, UpgradePreviewLineItem[] LineItems);

public sealed record UpgradePreviewLineItem(string Description, decimal Amount, string Currency, bool IsProration, bool IsTax);

public sealed record CheckoutPreviewResult(decimal TotalAmount, string Currency, decimal TaxAmount);

public sealed record PriceCatalogItem(
    SubscriptionPlan Plan,
    decimal UnitAmount,
    string Currency,
    string Interval,
    int IntervalCount,
    bool TaxInclusive
);

public static class PaystackSubscriptionStatus
{
    public const string Active = "active";
    public const string Incomplete = "non-renewing";
    public const string PastDue = "attention";
}
