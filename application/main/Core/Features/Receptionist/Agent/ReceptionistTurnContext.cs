using Main.Features.Clients.Domain;
using Main.Features.Receptionist.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Domain;

namespace Main.Features.Receptionist.Agent;

/// <summary>
///     Server-side state for a single receptionist turn. Tenant and customer identity live here — injected
///     from the webhook conversation state, never chosen by the model (spec §6.5.2). Tools read identity
///     from this context and expose only primitive parameters to the model. The context also carries the
///     per-turn guardrail counters (spec R9) and the outcome flags the turn handler acts on after the run.
/// </summary>
public sealed class ReceptionistTurnContext(
    WhatsAppBusinessAccount account,
    WhatsAppConversation conversation,
    ReceptionistSettings settings,
    SchedulingProfile profile,
    Client? client,
    DateTimeOffset now,
    int maxToolCallsPerTurn
)
{
    public WhatsAppBusinessAccount Account { get; } = account;

    public WhatsAppConversation Conversation { get; } = conversation;

    public ReceptionistSettings Settings { get; } = settings;

    public SchedulingProfile Profile { get; } = profile;

    /// <summary>The identified client, or null while the conversation is unidentified (write tools absent).</summary>
    public Client? Client { get; } = client;

    public DateTimeOffset Now { get; } = now;

    public TenantId TenantId => Account.TenantId;

    public string CustomerPhoneNumber => Conversation.CustomerPhoneNumber;

    public bool IsIdentified => Client is not null;

    /// <summary>Tenant-local timezone used to render slot times to the customer.</summary>
    public string TimeZone => "Africa/Johannesburg";

    public int ToolCallCount { get; private set; }

    public bool ToolBudgetExceeded { get; private set; }

    /// <summary>Set by the EscalateToHuman tool; the turn handler moves the session to Escalated after the run.</summary>
    public bool EscalationRequested { get; private set; }

    public string? EscalationReason { get; private set; }

    /// <summary>True when any tool call dispatched by this turn failed validation (used for telemetry only).</summary>
    public int FailedToolCallCount { get; private set; }

    /// <summary>Returns true while the turn is within its tool budget; false (and latches the flag) once exceeded.</summary>
    public bool TryConsumeToolBudget()
    {
        ToolCallCount++;
        if (ToolCallCount <= maxToolCallsPerTurn) return true;

        ToolBudgetExceeded = true;
        return false;
    }

    public void RequestEscalation(string reason)
    {
        EscalationRequested = true;
        EscalationReason = reason;
    }

    public void RecordFailedToolCall()
    {
        FailedToolCallCount++;
    }
}
