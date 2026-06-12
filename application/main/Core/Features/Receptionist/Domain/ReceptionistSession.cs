using JetBrains.Annotations;
using Main.Features.WhatsAppBooking.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Receptionist.Domain;

[PublicAPI]
[IdPrefix("rsess")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, ReceptionistSessionId>))]
public sealed record ReceptionistSessionId(string Value) : StronglyTypedUlid<ReceptionistSessionId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Persists the AI receptionist's state for a single <see cref="WhatsAppConversation" />: the serialized
///     agent thread (the conversation memory the model resumes from), token counters for budget enforcement
///     and billing, and the escalation state. The aggregate is the checkpoint — each turn loads the thread,
///     runs the agent, and persists the updated thread in the same unit of work (no durable runtime).
/// </summary>
public sealed class ReceptionistSession : AggregateRoot<ReceptionistSessionId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private ReceptionistSession() : base(ReceptionistSessionId.NewId())
    {
        WhatsAppConversationId = null!;
    }

    private ReceptionistSession(TenantId tenantId, WhatsAppConversationId whatsAppConversationId, DateTimeOffset now)
        : base(ReceptionistSessionId.NewId())
    {
        TenantId = tenantId;
        WhatsAppConversationId = whatsAppConversationId;
        State = ReceptionistSessionState.Active;
        LastTurnAt = now;
    }

    public WhatsAppConversationId WhatsAppConversationId { get; private set; }

    /// <summary>Serialized Microsoft Agent Framework thread (JSON). Null until the first turn completes.</summary>
    public string? AgentThread { get; private set; }

    public ReceptionistSessionState State { get; private set; }

    public int TurnCount { get; private set; }

    public long InputTokens { get; private set; }

    public long OutputTokens { get; private set; }

    public DateTimeOffset? LastTurnAt { get; private set; }

    /// <summary>True once the customer has been told a human will reply, so the hold message is sent at most once.</summary>
    public bool EscalationHoldNotified { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static ReceptionistSession Start(TenantId tenantId, WhatsAppConversationId whatsAppConversationId, DateTimeOffset now)
    {
        return new ReceptionistSession(tenantId, whatsAppConversationId, now);
    }

    /// <summary>Records a completed agent turn: the updated thread, token usage, and turn time.</summary>
    public void RecordTurn(string agentThread, long inputTokens, long outputTokens, DateTimeOffset now)
    {
        AgentThread = agentThread;
        InputTokens += inputTokens;
        OutputTokens += outputTokens;
        TurnCount++;
        LastTurnAt = now;
    }

    /// <summary>Moves the session to Escalated: the agent stops responding until a human resolves the escalation.</summary>
    public void Escalate()
    {
        State = ReceptionistSessionState.Escalated;
        EscalationHoldNotified = false;
    }

    /// <summary>Records that the one-time "a human will reply" hold message has been sent (spec R6 AC).</summary>
    public void MarkEscalationHoldNotified()
    {
        EscalationHoldNotified = true;
    }

    /// <summary>Returns an escalated session to active so the agent responds again.</summary>
    public void Resume()
    {
        State = ReceptionistSessionState.Active;
        EscalationHoldNotified = false;
    }

    /// <summary>Expires the session; the next turn starts a fresh thread.</summary>
    public void Expire()
    {
        State = ReceptionistSessionState.Expired;
        AgentThread = null;
    }

    /// <summary>Starts a fresh thread after expiry while keeping lifetime token counters.</summary>
    public void RestartThread(DateTimeOffset now)
    {
        State = ReceptionistSessionState.Active;
        AgentThread = null;
        LastTurnAt = now;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReceptionistSessionState
{
    Active,
    Escalated,
    Expired
}
