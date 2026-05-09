using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Subscriptions.Domain;

[PublicAPI]
[IdPrefix("payatt")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, PaystackPaymentAttemptId>))]
public sealed record PaystackPaymentAttemptId(string Value) : StronglyTypedUlid<PaystackPaymentAttemptId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class PaystackPaymentAttempt : AggregateRoot<PaystackPaymentAttemptId>, ITenantScopedEntity
{
    private PaystackPaymentAttempt(
        TenantId tenantId,
        SubscriptionId subscriptionId,
        string paystackReference,
        PaystackCustomerId paystackCustomerId,
        PaystackPaymentPurpose purpose,
        SubscriptionPlan? plan,
        decimal amount,
        string currency
    ) : base(PaystackPaymentAttemptId.NewId())
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        PaystackReference = paystackReference;
        PaystackCustomerId = paystackCustomerId;
        Purpose = purpose;
        Plan = plan;
        Amount = amount;
        Currency = currency;
        Status = PaystackPaymentAttemptStatus.Pending;
    }

    public SubscriptionId SubscriptionId { get; }

    public string PaystackReference { get; private set; }

    public PaystackCustomerId PaystackCustomerId { get; private set; }

    public PaystackSubscriptionId? PaystackAuthorizationCode { get; private set; }

    public PaystackPaymentPurpose Purpose { get; }

    public SubscriptionPlan? Plan { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public PaystackPaymentAttemptStatus Status { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public TenantId TenantId { get; }

    public static PaystackPaymentAttempt Create(
        TenantId tenantId,
        SubscriptionId subscriptionId,
        string paystackReference,
        PaystackCustomerId paystackCustomerId,
        PaystackSubscriptionId? paystackAuthorizationCode,
        PaystackPaymentPurpose purpose,
        SubscriptionPlan? plan,
        decimal amount,
        string currency
    )
    {
        return new PaystackPaymentAttempt(tenantId, subscriptionId, paystackReference, paystackCustomerId, purpose, plan, amount, currency)
        {
            PaystackAuthorizationCode = paystackAuthorizationCode
        };
    }

    public bool Matches(PaystackPaymentPurpose purpose, SubscriptionPlan? plan)
    {
        return Purpose == purpose && Plan == plan;
    }

    public bool MatchesAmount(decimal amount, string currency)
    {
        return decimal.Round(Amount, 2, MidpointRounding.AwayFromZero) == decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
               && string.Equals(Currency, currency, StringComparison.OrdinalIgnoreCase);
    }

    public void MarkSucceeded(DateTimeOffset completedAt)
    {
        Status = PaystackPaymentAttemptStatus.Succeeded;
        CompletedAt = completedAt;
        FailureReason = null;
    }

    public void MarkFailed(DateTimeOffset failedAt, string failureReason)
    {
        Status = PaystackPaymentAttemptStatus.Failed;
        CompletedAt = failedAt;
        FailureReason = failureReason;
    }
}

public enum PaystackPaymentAttemptStatus
{
    Pending,
    Succeeded,
    Failed
}
