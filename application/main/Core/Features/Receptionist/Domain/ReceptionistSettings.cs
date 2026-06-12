using System.Collections.Immutable;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Receptionist.Domain;

[PublicAPI]
[IdPrefix("rcset")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, ReceptionistSettingsId>))]
public sealed record ReceptionistSettingsId(string Value) : StronglyTypedUlid<ReceptionistSettingsId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Per-tenant configuration for the AI receptionist: the on/off switch the webhook router consults
///     (the kill switch — turning it off reverts to the deterministic Flows engine on the next message),
///     plus the persona inputs the owner controls: tone, languages, and free-text business notes/FAQ.
///     One row per tenant, created on first save.
/// </summary>
public sealed class ReceptionistSettings : AggregateRoot<ReceptionistSettingsId>, ITenantScopedEntity
{
    public const int MaxFaqNotesLength = 4000;

    [UsedImplicitly]
    private ReceptionistSettings() : base(ReceptionistSettingsId.NewId())
    {
    }

    private ReceptionistSettings(TenantId tenantId)
        : base(ReceptionistSettingsId.NewId())
    {
        TenantId = tenantId;
        Tone = ReceptionistTone.Friendly;
        Languages = ["English"];
    }

    /// <summary>When false, inbound free-text messages keep flowing to the deterministic Flows engine.</summary>
    public bool IsEnabled { get; private set; }

    public ReceptionistTone Tone { get; private set; }

    /// <summary>Languages the receptionist may respond in (e.g. English, Afrikaans, isiZulu, isiXhosa).</summary>
    public ImmutableArray<string> Languages { get; private set; }

    /// <summary>Owner-written business notes and FAQ answers the receptionist may use (treated as data, never instructions).</summary>
    public string? FaqNotes { get; private set; }

    /// <summary>The owner's personal WhatsApp number for digests and escalation notifications (E.164).</summary>
    public string? OwnerPhoneNumber { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static ReceptionistSettings Create(TenantId tenantId)
    {
        return new ReceptionistSettings(tenantId);
    }

    public void Update(bool isEnabled, ReceptionistTone tone, ImmutableArray<string> languages, string? faqNotes, string? ownerPhoneNumber = null)
    {
        IsEnabled = isEnabled;
        Tone = tone;
        Languages = languages.IsDefaultOrEmpty ? ["English"] : languages;
        FaqNotes = string.IsNullOrWhiteSpace(faqNotes) ? null : faqNotes.Trim();
        OwnerPhoneNumber = string.IsNullOrWhiteSpace(ownerPhoneNumber) ? null : ownerPhoneNumber.Trim();
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReceptionistTone
{
    Professional,
    Friendly,
    Playful
}
