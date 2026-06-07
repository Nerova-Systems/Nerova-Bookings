using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.WhatsAppBooking.Domain;

[PublicAPI]
[IdPrefix("wacon")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WhatsAppConversationId>))]
public sealed record WhatsAppConversationId(string Value) : StronglyTypedUlid<WhatsAppConversationId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Tracks the deterministic WhatsApp booking conversation for a single customer (identified by their
///     WhatsApp phone number) talking to a tenant's WhatsApp Business number. Booking details are captured
///     inside a native WhatsApp Flow, so the state machine is intentionally small: a session starts on first
///     contact, a Flow is sent (<see cref="WhatsAppConversationState.AwaitingFlowCompletion" />), and the
///     conversation is <see cref="WhatsAppConversationState.Confirmed" /> once the submitted Flow produces a
///     booking. Inactive sessions <see cref="WhatsAppConversationState.Expired" />. The full message
///     transcript and raw webhook payloads live on WhatsAppMessage / WhatsAppEvent respectively.
/// </summary>
public sealed class WhatsAppConversation : AggregateRoot<WhatsAppConversationId>, ITenantScopedEntity
{
    /// <summary>How long an incomplete booking session stays active before it is considered abandoned.</summary>
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(1);

    [UsedImplicitly]
    private WhatsAppConversation() : base(WhatsAppConversationId.NewId())
    {
        CustomerPhoneNumber = string.Empty;
    }

    private WhatsAppConversation(TenantId tenantId, string customerPhoneNumber, DateTimeOffset now)
        : base(WhatsAppConversationId.NewId())
    {
        TenantId = tenantId;
        CustomerPhoneNumber = customerPhoneNumber;
        State = WhatsAppConversationState.Idle;
        LastInboundAt = now;
        ExpiresAt = now + SessionTimeout;
    }

    public TenantId TenantId { get; } = new(0);

    /// <summary>The customer's WhatsApp phone number in E.164 format. Unique per tenant.</summary>
    public string CustomerPhoneNumber { get; private set; }

    public WhatsAppConversationState State { get; private set; }

    /// <summary>
    ///     Correlation token for the in-flight WhatsApp Flow. Echoed back in the flow-completion (nfm_reply)
    ///     webhook so the submission can be matched to this conversation. Null when no Flow awaits completion.
    /// </summary>
    public string? ActiveFlowToken { get; private set; }

    /// <summary>The booking produced by the completed Flow, once created.</summary>
    public BookingId? DraftBookingId { get; private set; }

    public DateTimeOffset LastInboundAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static WhatsAppConversation Start(TenantId tenantId, string customerPhoneNumber, DateTimeOffset now)
    {
        return new WhatsAppConversation(tenantId, customerPhoneNumber, now);
    }

    /// <summary>Records that a booking Flow has been sent and the conversation awaits the customer's submission.</summary>
    public void BeginFlow(string flowToken, DateTimeOffset now)
    {
        State = WhatsAppConversationState.AwaitingFlowCompletion;
        ActiveFlowToken = flowToken;
        DraftBookingId = null;
        LastInboundAt = now;
        ExpiresAt = now + SessionTimeout;
    }

    /// <summary>Extends the session on any inbound activity without changing the state.</summary>
    public void TouchInbound(DateTimeOffset now)
    {
        LastInboundAt = now;
        ExpiresAt = now + SessionTimeout;
    }

    /// <summary>Marks the conversation complete after the submitted Flow produced a booking.</summary>
    public void CompleteWithBooking(BookingId bookingId, DateTimeOffset now)
    {
        State = WhatsAppConversationState.Confirmed;
        DraftBookingId = bookingId;
        ActiveFlowToken = null;
        LastInboundAt = now;
    }

    /// <summary>Resets the conversation to a fresh session, e.g. when a returning customer starts a new booking.</summary>
    public void Restart(DateTimeOffset now)
    {
        State = WhatsAppConversationState.Idle;
        ActiveFlowToken = null;
        DraftBookingId = null;
        LastInboundAt = now;
        ExpiresAt = now + SessionTimeout;
    }

    /// <summary>Marks an abandoned (inactive) session as expired.</summary>
    public void Expire()
    {
        State = WhatsAppConversationState.Expired;
        ActiveFlowToken = null;
    }

    /// <summary>True when an in-progress session has passed its inactivity deadline.</summary>
    public bool HasExpired(DateTimeOffset now)
    {
        return State is WhatsAppConversationState.Idle or WhatsAppConversationState.AwaitingFlowCompletion
            && ExpiresAt < now;
    }

    /// <summary>True when the given Flow-completion token matches the in-flight Flow for this conversation.</summary>
    public bool MatchesFlowToken(string flowToken)
    {
        return ActiveFlowToken is not null && string.Equals(ActiveFlowToken, flowToken, StringComparison.Ordinal);
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WhatsAppConversationState
{
    Idle,
    AwaitingFlowCompletion,
    Confirmed,
    Expired
}
