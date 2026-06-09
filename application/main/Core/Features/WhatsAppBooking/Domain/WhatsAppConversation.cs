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
///     WhatsApp phone number) talking to a tenant's WhatsApp Business number. The state machine forks on
///     first contact: known customers go straight to booking; unknown customers go through an in-Flow login
///     step first.
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

    /// <summary>The customer's WhatsApp phone number in E.164 format. Unique per tenant.</summary>
    public string CustomerPhoneNumber { get; private set; }

    public WhatsAppConversationState State { get; private set; }

    /// <summary>
    ///     True once the customer has been matched to an existing Client record or has completed the login Flow.
    ///     Determines whether the engine skips the login step and goes straight to booking.
    /// </summary>
    public bool IsIdentified { get; private set; }

    /// <summary>
    ///     Correlation token for the in-flight WhatsApp Flow. Echoed back in the flow-completion (nfm_reply)
    ///     webhook so the submission can be matched to this conversation. Null when no Flow awaits completion.
    /// </summary>
    public string? ActiveFlowToken { get; private set; }

    /// <summary>The booking produced by the completed Flow, once created.</summary>
    public BookingId? DraftBookingId { get; private set; }

    public DateTimeOffset LastInboundAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static WhatsAppConversation Start(TenantId tenantId, string customerPhoneNumber, DateTimeOffset now)
    {
        return new WhatsAppConversation(tenantId, customerPhoneNumber, now);
    }

    /// <summary>Records that a booking Flow has been sent and the conversation awaits the customer's submission.</summary>
    public void BeginFlow(string flowToken, DateTimeOffset now)
    {
        State = WhatsAppConversationState.AwaitingBookingFlow;
        ActiveFlowToken = flowToken;
        DraftBookingId = null;
        LastInboundAt = now;
        ExpiresAt = now + SessionTimeout;
    }

    /// <summary>Records that a login Flow has been sent to an unidentified customer.</summary>
    public void BeginLoginFlow(string flowToken, DateTimeOffset now)
    {
        State = WhatsAppConversationState.AwaitingLoginFlow;
        ActiveFlowToken = flowToken;
        LastInboundAt = now;
        ExpiresAt = now + SessionTimeout;
    }

    /// <summary>Marks the customer as identified (matched to a Client or completed login Flow).</summary>
    public void MarkIdentified(DateTimeOffset now)
    {
        IsIdentified = true;
        ActiveFlowToken = null;
        State = WhatsAppConversationState.Idle;
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
        IsIdentified = false;
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
        return State is WhatsAppConversationState.Idle
                   or WhatsAppConversationState.AwaitingLoginFlow
                   or WhatsAppConversationState.AwaitingBookingFlow
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
    AwaitingLoginFlow,
    AwaitingBookingFlow,
    Confirmed,
    Expired
}
