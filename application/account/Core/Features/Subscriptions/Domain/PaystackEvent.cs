using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Subscriptions.Domain;

[PublicAPI]
[IdPrefix("evt")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, PaystackEventId>))]
public sealed record PaystackEventId(string Value) : StronglyTypedString<PaystackEventId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Durable Paystack webhook inbox row. Rows are inserted during webhook acknowledgement and then
///     transitioned by the Paystack processing phase once the matching payment attempt can be verified.
/// </summary>
public sealed class PaystackEvent : AggregateRoot<PaystackEventId>
{
    private PaystackEvent(PaystackEventId id) : base(id)
    {
        EventType = string.Empty;
        Status = PaystackEventStatus.Pending;
    }

    public string EventType { get; private set; }

    public PaystackEventStatus Status { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public PaystackCustomerId? PaystackCustomerId { get; private set; }

    public PaystackAuthorizationCode? PaystackAuthorizationCode { get; private set; }

    public string? PaystackReference { get; private set; }

    public TenantId? TenantId { get; private set; }

    public string? Payload { get; private set; }

    public string? Error { get; private set; }

    /// <summary>
    ///     Factory method for phase 1 webhook acknowledgment. Creates a Pending event that will be
    ///     batch-processed in phase 2. TenantId and PaystackAuthorizationCode are backfilled by phase 2
    ///     via <see cref="SetTenantId" /> and <see cref="SetPaystackAuthorizationCode" />.
    /// </summary>
    public static PaystackEvent Create(string paystackEventId, string eventType, PaystackCustomerId? paystackCustomerId, string? payload, string? paystackReference = null)
    {
        return new PaystackEvent(PaystackEventId.NewId(paystackEventId))
        {
            EventType = eventType,
            PaystackCustomerId = paystackCustomerId,
            PaystackReference = paystackReference,
            Payload = payload
        };
    }

    /// <summary>
    ///     Marks the event as successfully processed during phase 2 batch processing.
    /// </summary>
    public void MarkProcessed(DateTimeOffset processedAt)
    {
        EnsurePending();
        Status = PaystackEventStatus.Processed;
        ProcessedAt = processedAt;
    }

    /// <summary>
    ///     Marks the event as ignored during phase 1 when no customer ID is present.
    /// </summary>
    public void MarkIgnored(DateTimeOffset processedAt)
    {
        EnsurePending();
        Status = PaystackEventStatus.Ignored;
        ProcessedAt = processedAt;
    }

    /// <summary>
    ///     Marks the event as failed with an error message when phase 2 processing encounters an error.
    /// </summary>
    public void MarkFailed(DateTimeOffset failedAt, string error)
    {
        EnsurePending();
        Status = PaystackEventStatus.Failed;
        ProcessedAt = failedAt;
        Error = error;
    }

    public void SetPaystackAuthorizationCode(PaystackAuthorizationCode? paystackAuthorizationCode)
    {
        PaystackAuthorizationCode = paystackAuthorizationCode;
    }

    public void SetTenantId(TenantId? tenantId)
    {
        TenantId = tenantId;
    }

    private void EnsurePending()
    {
        if (Status is not PaystackEventStatus.Pending)
        {
            throw new InvalidOperationException($"PaystackEvent '{Id.Value}' is no longer Pending (status: {Status}); refusing to mutate.");
        }
    }
}
