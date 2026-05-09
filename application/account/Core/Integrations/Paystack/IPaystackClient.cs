using Account.Features.Subscriptions.Domain;
using JetBrains.Annotations;

namespace Account.Integrations.Paystack;

public interface IPaystackClient
{
    Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken);

    Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, PaystackPaymentPurpose purpose, CancellationToken cancellationToken);

    Task<AuthorizationChargeResult?> ChargeAuthorizationAsync(
        PaystackCustomerId paystackCustomerId,
        PaystackSubscriptionId authorizationCode,
        string email,
        PaystackPaymentPurpose purpose,
        SubscriptionPlan plan,
        decimal amount,
        string currency,
        CancellationToken cancellationToken
    );

    Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken);

    PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader);

    Task<VerifiedPaystackTransactionResult?> VerifyTransactionAsync(string reference, PaystackPaymentPurpose purpose, CancellationToken cancellationToken);

    Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken);

    Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken);

    Task<CheckoutSessionResult?> CreatePaymentMethodAuthorizationAsync(PaystackCustomerId paystackCustomerId, string email, CancellationToken cancellationToken);

    Task<VerifiedPaystackTransactionResult?> VerifyPaymentMethodAuthorizationAsync(string reference, CancellationToken cancellationToken);

    Task<RefundResult?> CreateRefundAsync(string transactionReference, decimal amount, string currency, CancellationToken cancellationToken);

    Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken);

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

public sealed record RefundResult(string? RefundId, decimal Amount, string Currency, string Status);

public sealed record CustomerBillingResult(BillingInfo? BillingInfo, bool IsCustomerDeleted, PaymentMethod? PaymentMethod = null);

public sealed record AuthorizationChargeResult(
    bool Paid,
    string Reference,
    decimal Amount,
    string Currency,
    PaymentMethod? PaymentMethod = null,
    string? ErrorMessage = null
);

public sealed record CheckoutPreviewResult(decimal TotalAmount, string Currency, decimal TaxAmount);

public sealed record PriceCatalogItem(
    SubscriptionPlan Plan,
    decimal UnitAmount,
    string Currency,
    string Interval,
    int IntervalCount,
    bool TaxInclusive
);

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaystackPaymentPurpose
{
    Subscribe,
    Upgrade,
    Retry,
    Renewal,
    PaymentMethodAuthorization
}
