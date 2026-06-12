using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.WhatsAppBooking.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Receptionist.Domain;

[PublicAPI]
[IdPrefix("escal")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, EscalationId>))]
public sealed record EscalationId(string Value) : StronglyTypedUlid<EscalationId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A conversation the AI receptionist handed to a human: the customer asked for something requiring
///     judgment (complaint, special request, budget breach, abuse). The escalation carries the reason and a
///     short conversation summary so the owner can resolve it in one read, and it is the single owner-facing
///     inbox item for everything that needs a human.
/// </summary>
public sealed class Escalation : AggregateRoot<EscalationId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Escalation() : base(EscalationId.NewId())
    {
        WhatsAppConversationId = null!;
        Reason = string.Empty;
        Summary = string.Empty;
    }

    private Escalation(TenantId tenantId, WhatsAppConversationId whatsAppConversationId, ClientId? clientId, string reason, string summary)
        : base(EscalationId.NewId())
    {
        TenantId = tenantId;
        WhatsAppConversationId = whatsAppConversationId;
        ClientId = clientId;
        Reason = reason;
        Summary = summary;
        Status = EscalationStatus.Open;
    }

    public WhatsAppConversationId WhatsAppConversationId { get; private set; }

    public ClientId? ClientId { get; private set; }

    public string Reason { get; private set; }

    public string Summary { get; private set; }

    public EscalationStatus Status { get; private set; }

    public UserId? ResolvedByUserId { get; private set; }

    public string? ResolutionNote { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static Escalation Create(TenantId tenantId, WhatsAppConversationId whatsAppConversationId, ClientId? clientId, string reason, string summary)
    {
        return new Escalation(tenantId, whatsAppConversationId, clientId, reason, summary);
    }

    public void Resolve(UserId resolvedByUserId, string? resolutionNote)
    {
        Status = EscalationStatus.Resolved;
        ResolvedByUserId = resolvedByUserId;
        ResolutionNote = resolutionNote;
    }

    public void Dismiss(UserId resolvedByUserId)
    {
        Status = EscalationStatus.Dismissed;
        ResolvedByUserId = resolvedByUserId;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EscalationStatus
{
    Open,
    Resolved,
    Dismissed
}
