using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Attributes.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="Attribute" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>attr</c>.
/// </summary>
[PublicAPI]
[IdPrefix("attr")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AttributeId>))]
public sealed record AttributeId(string Value) : StronglyTypedUlid<AttributeId>(Value)
{
    public override string ToString() => Value;
}

/// <summary>
///     An organisation-defined custom field that can be attached to memberships to categorise or
///     annotate org members (e.g. "Department", "Location", "Skill level").
///     <para>
///         Invariant: <see cref="Slug" /> is unique within an organisation's set of attributes.
///         Slug is derived from <see cref="Name" /> at creation time and never mutated.
///     </para>
///     <para>
///         Mirrors the <c>Attribute</c> model in the cal.com Prisma schema
///         (<see href="https://github.com/calcom/cal.com/blob/main/packages/prisma/schema.prisma" />).
///     </para>
/// </summary>
public sealed class Attribute : AggregateRoot<AttributeId>, ITenantScopedEntity
{
    // List<T> is used (not HashSet<T>) so that EF Core can populate the collection via the
    // backing field during OwnsMany materialisation. Uniqueness enforced by DB index.
    private readonly List<AttributeOption> _options = [];

    private Attribute(AttributeId id) : base(id) { }

    /// <summary>
    ///     The organisation this attribute belongs to (maps to an <see cref="TenantKind.Organization" />
    ///     tenant). Never changes after creation.
    /// </summary>
    public TenantId TenantId { get; private set; } = null!;

    /// <summary>Human-readable display name (e.g. <c>"Department"</c>).</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     URL-safe slug derived from <see cref="Name" /> at creation time (e.g. <c>"department"</c>).
    ///     Unique within the organisation. Never mutated after creation.
    /// </summary>
    public string Slug { get; private set; } = null!;

    /// <summary>Determines how assignment values are stored and validated.</summary>
    public AttributeType Type { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, assignments carry a numeric weight that the
    ///     round-robin scheduler uses to distribute bookings unevenly.
    /// </summary>
    public bool IsWeightsEnabled { get; private set; }

    /// <summary>
    ///     When <see langword="false" />, org members may not self-edit their assignments for this
    ///     attribute — only admins/owners can.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, self-editing is blocked even if <see cref="Enabled" /> is
    ///     <see langword="true" />. Only admins/owners can assign values.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    ///     Selectable options for <see cref="AttributeType.SingleSelect" /> /
    ///     <see cref="AttributeType.MultiSelect" /> attributes. Empty for Text / Number types.
    /// </summary>
    public IReadOnlyCollection<AttributeOption> Options => _options.AsReadOnly();

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new attribute for the given organisation tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="tenantKind" /> is not <see cref="TenantKind.Organization" />.
    /// </exception>
    public static Attribute Create(TenantId orgTenantId, TenantKind tenantKind, string name, AttributeType type)
    {
        if (tenantKind != TenantKind.Organization)
            throw new InvalidOperationException("Attributes can only be created for organization tenants.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Attribute(AttributeId.NewId())
        {
            TenantId = orgTenantId,
            Name = name,
            Slug = GenerateSlug(name),
            Type = type,
            IsWeightsEnabled = false,
            Enabled = true,
            IsLocked = false
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    /// <summary>Updates the mutable fields of this attribute.</summary>
    public void Update(string name, bool isLocked, bool isWeightsEnabled, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        IsLocked = isLocked;
        IsWeightsEnabled = isWeightsEnabled;
        Enabled = enabled;
    }

    /// <summary>
    ///     Appends a new option to this attribute and returns it.
    ///     Only valid for <see cref="AttributeType.SingleSelect" /> /
    ///     <see cref="AttributeType.MultiSelect" /> attributes.
    /// </summary>
    public AttributeOption AddOption(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var option = AttributeOption.Create(value);
        _options.Add(option);
        return option;
    }

    /// <summary>
    ///     Updates an existing option. Returns <see langword="false" /> if the option was not found.
    /// </summary>
    public bool UpdateOption(AttributeOptionId optionId, string value, bool isGroup, string[] contains)
    {
        var option = _options.Find(o => o.Id == optionId);
        if (option is null) return false;
        option.Update(value, isGroup, contains);
        return true;
    }

    /// <summary>
    ///     Removes an option by ID. Returns <see langword="false" /> if the option was not found.
    /// </summary>
    public bool RemoveOption(AttributeOptionId optionId)
    {
        var option = _options.Find(o => o.Id == optionId);
        if (option is null) return false;
        _options.Remove(option);
        return true;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Trim().Replace(' ', '-').Replace("_", "-");
}

/// <summary>
///     Persistence contract for <see cref="Attribute" /> aggregates.
/// </summary>
public interface IAttributeRepository : ICrudRepository<Attribute, AttributeId>
{
    /// <summary>Returns all attributes that belong to the given organisation, with their options.</summary>
    Task<IReadOnlyList<Attribute>> GetByOrgUnfilteredAsync(TenantId orgTenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Loads a single attribute by ID, bypassing the tenant query filter. Options are included.
    ///     Returns <see langword="null" /> if not found.
    /// </summary>
    Task<Attribute?> GetByIdUnfilteredAsync(AttributeId id, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns <see langword="true" /> if a slug already exists within the given org (for
    ///     duplicate-slug validation before create).
    /// </summary>
    Task<bool> SlugExistsUnfilteredAsync(TenantId orgTenantId, string slug, CancellationToken cancellationToken);
}
