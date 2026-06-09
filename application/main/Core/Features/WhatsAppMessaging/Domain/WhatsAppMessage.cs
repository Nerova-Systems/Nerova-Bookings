using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.WhatsAppMessaging.Domain;

[PublicAPI]
[IdPrefix("wamsg")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WhatsAppMessageId>))]
public sealed record WhatsAppMessageId(string Value) : StronglyTypedUlid<WhatsAppMessageId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class WhatsAppMessage : AggregateRoot<WhatsAppMessageId>, ITenantScopedEntity
{
    private WhatsAppMessage(TenantId tenantId) : base(WhatsAppMessageId.NewId())
    {
        TenantId = tenantId;
    }

    /// <summary>
    ///     The external Meta message ID (wamid.*). Indexed for lookups during status updates.
    /// </summary>
    public string MetaMessageId { get; private set; } = null!;

    public MessageDirection Direction { get; private set; }

    /// <summary>
    ///     Phone number of the sender. E.164 format from Meta.
    /// </summary>
    public string FromPhoneNumber { get; private set; } = null!;

    /// <summary>
    ///     Phone number of the recipient. E.164 format.
    /// </summary>
    public string ToPhoneNumber { get; private set; } = null!;

    public string Text { get; private set; } = null!;

    public WhatsAppMessageStatus Status { get; private set; }

    /// <summary>
    ///     The timestamp reported by Meta in the webhook payload (Unix epoch seconds, converted to UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    public TenantId TenantId { get; }

    public static WhatsAppMessage CreateInbound(TenantId tenantId, string metaMessageId, string fromPhoneNumber, string toPhoneNumber, string text, DateTimeOffset timestamp)
    {
        return new WhatsAppMessage(tenantId)
        {
            MetaMessageId = metaMessageId,
            Direction = MessageDirection.Inbound,
            FromPhoneNumber = fromPhoneNumber,
            ToPhoneNumber = toPhoneNumber,
            Text = text,
            Status = WhatsAppMessageStatus.Received,
            Timestamp = timestamp
        };
    }

    public static WhatsAppMessage CreateOutbound(TenantId tenantId, string metaMessageId, string fromPhoneNumber, string toPhoneNumber, string text, DateTimeOffset timestamp)
    {
        return new WhatsAppMessage(tenantId)
        {
            MetaMessageId = metaMessageId,
            Direction = MessageDirection.Outbound,
            FromPhoneNumber = fromPhoneNumber,
            ToPhoneNumber = toPhoneNumber,
            Text = text,
            Status = WhatsAppMessageStatus.Sent,
            Timestamp = timestamp
        };
    }

    public void UpdateStatus(WhatsAppMessageStatus status)
    {
        Status = status;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageDirection
{
    Inbound,
    Outbound
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WhatsAppMessageStatus
{
    Received,
    Sent,
    Delivered,
    Read,
    Failed
}
