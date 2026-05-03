using System.Collections.Immutable;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Subscriptions.Domain;

[PublicAPI]
[IdPrefix("sub")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, SubscriptionId>))]
public sealed record SubscriptionId(string Value) : StronglyTypedUlid<SubscriptionId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[PublicAPI]
[IdPrefix("pymnt")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, PaymentTransactionId>))]
public sealed record PaymentTransactionId(string Value) : StronglyTypedUlid<PaymentTransactionId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class Subscription : AggregateRoot<SubscriptionId>, ITenantScopedEntity
{
    private Subscription(TenantId tenantId) : base(SubscriptionId.NewId())
    {
        TenantId = tenantId;
        Plan = SubscriptionPlan.Basis;
        PaymentTransactions = ImmutableArray<PaymentTransaction>.Empty;
    }

    public SubscriptionPlan Plan { get; private set; }

    public SubscriptionPlan? ScheduledPlan { get; private set; }

    public PaystackCustomerId? PaystackCustomerId { get; private set; }

    public PaystackSubscriptionId? PaystackSubscriptionId { get; private set; }

    public decimal? CurrentPriceAmount { get; private set; }

    public string? CurrentPriceCurrency { get; private set; }

    public DateTimeOffset? CurrentPeriodEnd { get; private set; }

    public bool CancelAtPeriodEnd { get; private set; }

    public DateTimeOffset? FirstPaymentFailedAt { get; private set; }

    public CancellationReason? CancellationReason { get; private set; }

    public string? CancellationFeedback { get; private set; }

    public ImmutableArray<PaymentTransaction> PaymentTransactions { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public BillingInfo? BillingInfo { get; private set; }

    public TenantId TenantId { get; }

    public static Subscription Create(TenantId tenantId)
    {
        return new Subscription(tenantId);
    }

    public void SetPaystackCustomerId(PaystackCustomerId paystackCustomerId)
    {
        PaystackCustomerId = paystackCustomerId;
    }

    public void SetBillingInfo(BillingInfo? billingInfo)
    {
        BillingInfo = billingInfo;
    }

    public void SetPaystackSubscription(PaystackSubscriptionId? paystackSubscriptionId, SubscriptionPlan plan, decimal? currentPriceAmount, string? currentPriceCurrency, DateTimeOffset? currentPeriodEnd, PaymentMethod? paymentMethod)
    {
        PaystackSubscriptionId = paystackSubscriptionId;
        Plan = plan;
        CurrentPriceAmount = currentPriceAmount;
        CurrentPriceCurrency = currentPriceCurrency;
        CurrentPeriodEnd = currentPeriodEnd;
        PaymentMethod = paymentMethod;
    }

    public void SetCancellation(bool cancelAtPeriodEnd, CancellationReason? cancellationReason, string? cancellationFeedback)
    {
        CancelAtPeriodEnd = cancelAtPeriodEnd;
        CancellationReason = cancellationReason;
        CancellationFeedback = cancellationFeedback;
    }

    public void SetScheduledPlan(SubscriptionPlan? scheduledPlan)
    {
        ScheduledPlan = scheduledPlan;
    }

    public void SetPaymentTransactions(ImmutableArray<PaymentTransaction> paymentTransactions)
    {
        PaymentTransactions = paymentTransactions;
    }

    public void SetPaymentMethod(PaymentMethod? paymentMethod)
    {
        PaymentMethod = paymentMethod;
    }

    public void SetPaymentFailed(DateTimeOffset failedAt)
    {
        FirstPaymentFailedAt = failedAt;
    }

    public void ClearPaymentFailure()
    {
        FirstPaymentFailedAt = null;
    }

    public void ResetToFreePlan()
    {
        Plan = SubscriptionPlan.Basis;
        ScheduledPlan = null;
        PaystackSubscriptionId = null;
        CurrentPriceAmount = null;
        CurrentPriceCurrency = null;
        CurrentPeriodEnd = null;
        CancelAtPeriodEnd = false;
        FirstPaymentFailedAt = null;
        CancellationReason = null;
        CancellationFeedback = null;
    }

    public bool HasActivePaystackSubscription()
    {
        return PaystackSubscriptionId is not null && Plan != SubscriptionPlan.Basis && !CancelAtPeriodEnd;
    }
}

[PublicAPI]
public sealed record BillingAddress(
    string? Line1,
    string? Line2,
    string? PostalCode,
    string? City,
    string? State,
    string? Country
);

[PublicAPI]
public sealed record BillingInfo(string? Name, BillingAddress? Address, string? Email, string? TaxId);

[PublicAPI]
public sealed record PaymentMethod(string Brand, string Last4, int ExpMonth, int ExpYear);

[PublicAPI]
public sealed record PaymentTransaction(
    PaymentTransactionId Id,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    DateTimeOffset Date,
    string? FailureReason,
    string? InvoiceUrl,
    string? CreditNoteUrl
);
