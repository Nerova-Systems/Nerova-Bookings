using Account.Features.Subscriptions.Domain;
using JetBrains.Annotations;

namespace Account.Integrations.Paystack;

public interface IPaystackClient
{
    Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken);

    Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, PaystackPaymentPurpose purpose, CancellationToken cancellationToken);

    Task<SubscriptionSyncResult?> SyncSubscriptionStateAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    Task<PaystackSubscriptionId?> GetCheckoutSessionSubscriptionIdAsync(string sessionId, CancellationToken cancellationToken);

    Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan newPlan, CancellationToken cancellationToken);

    Task<bool> ScheduleDowngradeAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken);

    Task<bool> CancelScheduledDowngradeAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken);

    Task<bool> CancelSubscriptionAtPeriodEndAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationReason reason, string? feedback, CancellationToken cancellationToken);

    Task<bool> ReactivateSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken);

    Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken);

    PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader);

    Task<VerifiedPaystackTransactionResult?> VerifyTransactionAsync(string reference, PaystackPaymentPurpose purpose, CancellationToken cancellationToken);

    Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken);

    Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken);

    Task<CheckoutSessionResult?> CreateSetupIntentAsync(PaystackCustomerId paystackCustomerId, string email, CancellationToken cancellationToken);

    Task<VerifiedPaystackTransactionResult?> GetSetupIntentPaymentMethodAsync(string setupIntentId, CancellationToken cancellationToken);

    Task<bool> SetSubscriptionDefaultPaymentMethodAsync(PaystackSubscriptionId paystackSubscriptionId, string paymentMethodId, CancellationToken cancellationToken);

    Task<bool> SetCustomerDefaultPaymentMethodAsync(PaystackCustomerId paystackCustomerId, string paymentMethodId, CancellationToken cancellationToken);

    Task<OpenInvoiceResult?> GetOpenInvoiceAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken);

    Task<InvoiceRetryResult?> RetryOpenInvoicePaymentAsync(PaystackSubscriptionId paystackSubscriptionId, string? paymentMethodId, CancellationToken cancellationToken);

    Task<UpgradePreviewResult?> GetUpgradePreviewAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken);

    Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken);

    Task<SubscribeResult?> CreateSubscriptionWithSavedPaymentMethodAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan plan, CancellationToken cancellationToken);

    Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);
}

public sealed record PaystackWebhookEventResult(
    string EventId,
    string EventType,
    PaystackCustomerId? CustomerId,
    string? Reference = null
);

public sealed record CheckoutSessionResult(
    string Reference,
    string AccessCode,
    decimal Amount,
    string Currency,
    PaystackPaymentPurpose Purpose
);

public sealed record VerifiedPaystackTransactionResult(
    string Reference,
    decimal Amount,
    string Currency,
    bool Paid,
    PaystackPaymentPurpose Purpose,
    PaystackCustomerId? CustomerId,
    PaystackAuthorization? Authorization,
    PaymentMethod? PaymentMethod,
    string? ErrorMessage = null
);

public sealed record PaystackAuthorization(
    PaystackSubscriptionId AuthorizationCode,
    string Email,
    string Signature
);

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

public sealed record InvoiceRetryResult(
    bool Paid,
    string? ErrorMessage = null,
    string? AccessCode = null,
    string? Reference = null,
    decimal? Amount = null,
    string? Currency = null
);

public sealed record UpgradeSubscriptionResult(string? ErrorMessage = null, string? AccessCode = null, string? Reference = null, decimal? Amount = null, string? Currency = null);

public sealed record SubscribeResult(string Reference, decimal Amount, string Currency, bool Paid, PaymentMethod? PaymentMethod = null);

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
    public const string Incomplete = "incomplete";
    public const string PastDue = "past_due";
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaystackPaymentPurpose
{
    Subscribe,
    Upgrade,
    Retry,
    PaymentMethodAuthorization
}
