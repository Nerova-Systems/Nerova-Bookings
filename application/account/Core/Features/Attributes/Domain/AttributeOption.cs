using JetBrains.Annotations;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Attributes.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="AttributeOption" /> owned entity.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>atop</c>.
/// </summary>
[PublicAPI]
[IdPrefix("atop")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AttributeOptionId>))]
public sealed record AttributeOptionId(string Value) : StronglyTypedUlid<AttributeOptionId>(Value)
{
    public override string ToString() => Value;
}

/// <summary>
///     A selectable value for a <see cref="Attribute" /> whose <see cref="Attribute.Type" /> is
///     <see cref="AttributeType.SingleSelect" /> or <see cref="AttributeType.MultiSelect" />.
///     Owned by its parent <see cref="Attribute" /> and stored in the <c>attribute_options</c> table
///     via <c>OwnsMany</c>. Mirrors <c>AttributeOption</c> in the cal.com Prisma schema.
/// </summary>
public sealed class AttributeOption
{
    // Private constructor for factory + EF materialisation.
    private AttributeOption() { }

    /// <summary>Unique identifier; auto-generated ULID with <c>atop_</c> prefix.</summary>
    public AttributeOptionId Id { get; private set; } = null!;

    /// <summary>Human-readable label shown in the UI (e.g. <c>"Engineering"</c>).</summary>
    public string Value { get; private set; } = null!;

    /// <summary>
    ///     URL-safe slug derived from <see cref="Value" /> (e.g. <c>"engineering"</c>).
    ///     Unique within an attribute. Auto-generated from <see cref="Value" /> on creation.
    /// </summary>
    public string Slug { get; private set; } = null!;

    /// <summary>
    ///     When <see langword="true" /> this option is a group header that contains child options
    ///     identified by their slugs in <see cref="Contains" />.
    /// </summary>
    public bool IsGroup { get; private set; }

    /// <summary>
    ///     Slugs (or IDs) of child option entries when <see cref="IsGroup" /> is
    ///     <see langword="true" />. Stored as a JSON text column. Maps to cal.com
    ///     <c>AttributeOption.contains</c>.
    /// </summary>
    public string[] Contains { get; private set; } = [];

    // ─── Factory ──────────────────────────────────────────────────────────────

    internal static AttributeOption Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new AttributeOption
        {
            Id = AttributeOptionId.NewId(),
            Value = value,
            Slug = GenerateSlug(value),
            IsGroup = false,
            Contains = []
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    internal void Update(string value, bool isGroup, string[] contains)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        IsGroup = isGroup;
        Contains = contains;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateSlug(string value) =>
        value.ToLowerInvariant().Trim().Replace(' ', '-').Replace("_", "-");
}
