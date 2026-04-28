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
    private Subscription(TenantId tenantId, DateTimeOffset trialEndsAt) : base(SubscriptionId.NewId())
    {
        TenantId = tenantId;
        Status = SubscriptionStatus.Trial;
        Plan = SubscriptionPlan.Trial;
        TrialEndsAt = trialEndsAt;
        PaymentTransactions = ImmutableArray<PaymentTransaction>.Empty;
    }

    public SubscriptionStatus Status { get; private set; }

    public SubscriptionPlan Plan { get; private set; }

    public SubscriptionPlan? ScheduledPlan { get; private set; }

    public string? PayFastToken { get; private set; }

    public string? PayFastPaymentId { get; private set; }

    public DateTimeOffset TrialEndsAt { get; private set; }

    public DateTimeOffset? NextBillingDate { get; private set; }

    public DateTimeOffset? CurrentPeriodStart { get; private set; }

    public DateTimeOffset? CurrentPeriodEnd { get; private set; }

    public DateTimeOffset? FirstPaymentFailedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public CancellationReason? CancellationReason { get; private set; }

    public string? CancellationFeedback { get; private set; }

    public ImmutableArray<PaymentTransaction> PaymentTransactions { get; private set; }

    public BillingInfo? BillingInfo { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public TenantId TenantId { get; }

    public static Subscription Create(TenantId tenantId, DateTimeOffset now)
    {
        return new Subscription(tenantId, now.AddDays(30));
    }

    public void SetScheduledPlan(SubscriptionPlan? scheduledPlan)
    {
        ScheduledPlan = scheduledPlan;
    }

    public void SetPaymentTransactions(ImmutableArray<PaymentTransaction> paymentTransactions)
    {
        PaymentTransactions = paymentTransactions;
    }

    public bool TryRecordRefund(
        PaymentTransactionId transactionId,
        decimal amount,
        string reason,
        string? creditNoteUrl,
        string? refundReference
    )
    {
        var index = -1;
        for (var i = 0; i < PaymentTransactions.Length; i++)
        {
            if (PaymentTransactions[i].Id == transactionId)
            {
                index = i;
                break;
            }
        }
        if (index < 0) return false;

        var transaction = PaymentTransactions[index];
        var refundedAmount = transaction.RefundedAmount + amount;
        var status = refundedAmount >= transaction.Amount
            ? PaymentTransactionStatus.Refunded
            : transaction.Status;

        var updatedTransaction = transaction with
        {
            Status = status,
            CreditNoteUrl = creditNoteUrl ?? transaction.CreditNoteUrl,
            RefundedAmount = refundedAmount,
            RefundReason = reason,
            RefundReference = refundReference
        };

        PaymentTransactions = PaymentTransactions.SetItem(index, updatedTransaction);
        return true;
    }

    public void SetPaymentFailed(DateTimeOffset failedAt)
    {
        FirstPaymentFailedAt = failedAt;
    }

    public void ClearPaymentFailure()
    {
        FirstPaymentFailedAt = null;
    }

    public void Activate(SubscriptionPlan plan, string? payFastToken, string payFastPaymentId, DateTimeOffset now)
    {
        Status = SubscriptionStatus.Active;
        Plan = plan;
        ScheduledPlan = null;
        if (payFastToken is not null) PayFastToken = payFastToken;
        PayFastPaymentId = payFastPaymentId;
        NextBillingDate = now.AddDays(30);
        CurrentPeriodStart = now;
        CurrentPeriodEnd = now.AddDays(30);
        FirstPaymentFailedAt = null;
        CancelledAt = null;
        CancellationReason = null;
        CancellationFeedback = null;
    }

    public void SetPlan(SubscriptionPlan plan)
    {
        Plan = plan;
    }

    public void RenewBillingPeriod(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = now;
        CurrentPeriodEnd = now.AddDays(30);
        NextBillingDate = now.AddDays(30);
        FirstPaymentFailedAt = null;

        if (ScheduledPlan is not null)
        {
            Plan = ScheduledPlan.Value;
            ScheduledPlan = null;
        }
    }

    public void SetPastDue(DateTimeOffset failedAt)
    {
        Status = SubscriptionStatus.PastDue;
        FirstPaymentFailedAt ??= failedAt;
    }

    public void Cancel(CancellationReason? reason, string? feedback, DateTimeOffset now)
    {
        Status = SubscriptionStatus.Cancelled;
        CancellationReason = reason;
        CancellationFeedback = feedback;
        CancelledAt = now;
    }

    /// <summary>
    ///     Reactivates a cancelled subscription within its still-paid billing period. The user already
    ///     paid for the current period, so no new charge is taken — we just clear the cancellation
    ///     flags and restore Active status. The existing CurrentPeriodEnd / NextBillingDate are kept.
    /// </summary>
    public void ResumeWithinPaidPeriod()
    {
        Status = SubscriptionStatus.Active;
        CancelledAt = null;
        CancellationReason = null;
        CancellationFeedback = null;
    }

    public void Expire()
    {
        Status = SubscriptionStatus.Expired;
        PayFastToken = null;
        PayFastPaymentId = null;
        PaymentMethod = null;
    }

    public void SetBillingInfo(BillingInfo billingInfo)
    {
        BillingInfo = billingInfo;
    }

    public void SetPaymentMethod(PaymentMethod paymentMethod)
    {
        PaymentMethod = paymentMethod;
    }

    public void ClearPaymentMethod()
    {
        PaymentMethod = null;
    }
}

[PublicAPI]
public sealed record PaymentTransaction(
    PaymentTransactionId Id,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    DateTimeOffset Date,
    string? FailureReason,
    string? InvoiceUrl,
    string? CreditNoteUrl,
    string? Provider = null,
    string? ProviderPaymentId = null,
    string? ProviderEventId = null,
    string? ProviderStatus = null,
    string? RawPayloadJson = null,
    decimal RefundedAmount = 0,
    string? RefundReason = null,
    string? RefundReference = null
);

/// <summary>
///     Billing details displayed on invoices and the billing page. Stored locally as a JSONB column on
///     the Subscription aggregate (PayFast does not store these — invoicing is our responsibility).
/// </summary>
[PublicAPI]
public sealed record BillingInfo(
    string Name,
    BillingAddress Address,
    string Email,
    string? TaxId
);

[PublicAPI]
public sealed record BillingAddress(
    string Line1,
    string? Line2,
    string PostalCode,
    string City,
    string? State,
    string Country
);

/// <summary>
///     Payment method summary shown on the billing page. PayFast does not return card brand / last4 /
///     expiry from the recurring API, so we populate <c>Brand = "Card on file"</c> and leave the other
///     fields null. The shape is preserved for UI compatibility with the billing page layout.
/// </summary>
[PublicAPI]
public sealed record PaymentMethod(
    string Brand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear
);
