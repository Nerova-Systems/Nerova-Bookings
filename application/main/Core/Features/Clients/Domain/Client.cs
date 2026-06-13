using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Main.Features.Clients.Domain;

/// <summary>
///     A booking customer (client) belonging to a tenant. Clients are created when a customer books
///     or is added manually. The aggregate's <c>CreatedAt</c> represents the client's first visit and
///     <see cref="LastVisitAt" /> the most recent one. A client missing an email or phone number is
///     flagged via <see cref="NeedsAttention" /> so staff can complete the record.
/// </summary>
public sealed class Client : AggregateRoot<ClientId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Client() : base(ClientId.NewId())
    {
    }

    private Client(TenantId tenantId, string firstName, string lastName, string? email, string? phoneNumber)
        : base(ClientId.NewId())
    {
        TenantId = tenantId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = Normalize(email)?.ToLowerInvariant();
        PhoneNumber = Normalize(phoneNumber);
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? AvatarUrl { get; private set; }

    /// <summary>Free-text notes about the client, e.g. imported remarks from a previous system.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    ///     Standard + Constraint vertical field values keyed by catalog key (jsonb). Typed access only —
    ///     use <see cref="SetVerticalField" /> / <see cref="GetVerticalFields" /> so every write is
    ///     validated against <see cref="VerticalFieldCatalog" /> before it lands here.
    /// </summary>
    private Dictionary<string, string> VerticalFields { get; set; } = new();

    /// <summary>
    ///     Encrypted JSON payload of Sensitive-class field values (docs/vertical-template-fields-spec.md §3).
    ///     Written and read only through <c>FieldProtector</c>-aware command/query paths; never exposed
    ///     to agents, telemetry, or logs.
    /// </summary>
    public string? SensitiveFields { get; private set; }

    public DateTimeOffset? LastVisitAt { get; private set; }

    public bool NeedsAttention => string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(PhoneNumber);

    public TenantId TenantId { get; } = new(0);

    public static Client Create(TenantId tenantId, string firstName, string lastName, string? email, string? phoneNumber)
    {
        return new Client(tenantId, firstName, lastName, email, phoneNumber);
    }

    public void Update(string firstName, string lastName, string? email, string? phoneNumber)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = Normalize(email)?.ToLowerInvariant();
        PhoneNumber = Normalize(phoneNumber);
    }

    public void UpdateAvatar(string? avatarUrl)
    {
        AvatarUrl = Normalize(avatarUrl);
    }

    public void UpdateNotes(string? notes)
    {
        Notes = Normalize(notes);
    }

    public void RecordVisit(DateTimeOffset visitedAt)
    {
        LastVisitAt = visitedAt;
    }

    /// <summary>Returns a read-only snapshot of the Standard + Constraint vertical field values.</summary>
    public IReadOnlyDictionary<string, string> GetVerticalFields()
    {
        return VerticalFields;
    }

    /// <summary>
    ///     Sets or clears (null/whitespace value) one vertical field. The caller is responsible for
    ///     catalog validation; this method only maintains the storage invariant that empty values are
    ///     absent keys, never empty strings.
    /// </summary>
    public void SetVerticalField(string key, string? value)
    {
        // EF change tracking compares the dictionary reference for jsonb conversions — replace, never mutate.
        var updated = new Dictionary<string, string>(VerticalFields);
        if (string.IsNullOrWhiteSpace(value))
        {
            updated.Remove(key);
        }
        else
        {
            updated[key] = value.Trim();
        }

        VerticalFields = updated;
    }

    /// <summary>Replaces the encrypted sensitive-fields payload (already protected by <c>FieldProtector</c>).</summary>
    public void SetSensitiveFieldsPayload(string? encryptedPayload)
    {
        SensitiveFields = string.IsNullOrWhiteSpace(encryptedPayload) ? null : encryptedPayload;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
