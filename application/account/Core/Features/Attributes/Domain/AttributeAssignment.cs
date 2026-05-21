using Account.Features.Memberships.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Attributes.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="AttributeAssignment" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>atas</c>.
/// </summary>
[PublicAPI]
[IdPrefix("atas")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AttributeAssignmentId>))]
public sealed record AttributeAssignmentId(string Value) : StronglyTypedUlid<AttributeAssignmentId>(Value)
{
    public override string ToString() => Value;
}

/// <summary>
///     Links a <see cref="Membership" /> to a specific <see cref="Attribute" /> value or option.
///     Exactly one of <see cref="Value" /> or <see cref="AttributeOptionId" /> is set, depending on
///     the parent attribute's <see cref="AttributeType" />.
///     <para>
///         Mirrors <c>AttributeToUser</c> in the cal.com Prisma schema.
///     </para>
/// </summary>
public sealed class AttributeAssignment : AggregateRoot<AttributeAssignmentId>, ITenantScopedEntity
{
    private AttributeAssignment(AttributeAssignmentId id) : base(id) { }

    /// <summary>
    ///     The org tenant this assignment belongs to. Stored explicitly for efficient filtering
    ///     without joining through <see cref="Attribute" /> or <see cref="Membership" />.
    /// </summary>
    public TenantId TenantId { get; private set; } = null!;

    /// <summary>The membership (org member) this assignment targets.</summary>
    public MembershipId MembershipId { get; private set; } = null!;

    /// <summary>The attribute this assignment is for.</summary>
    public AttributeId AttributeId { get; private set; } = null!;

    /// <summary>
    ///     Free-text value for <see cref="AttributeType.Text" /> and
    ///     <see cref="AttributeType.Number" /> attributes. <see langword="null" /> for select types.
    /// </summary>
    public string? Value { get; private set; }

    /// <summary>
    ///     Selected option ID for <see cref="AttributeType.SingleSelect" /> and
    ///     <see cref="AttributeType.MultiSelect" /> attributes. <see langword="null" /> for text/number.
    /// </summary>
    public AttributeOptionId? AttributeOptionId { get; private set; }

    /// <summary>
    ///     Round-robin scheduling weight. Only meaningful when the parent attribute has
    ///     <c>IsWeightsEnabled = true</c>.
    /// </summary>
    public int? Weight { get; private set; }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>Creates a new assignment linking a membership to an attribute value or option.</summary>
    public static AttributeAssignment Create(
        TenantId orgTenantId,
        MembershipId membershipId,
        AttributeId attributeId,
        AttributeOptionId? optionId,
        string? value,
        int? weight)
    {
        return new AttributeAssignment(AttributeAssignmentId.NewId())
        {
            TenantId = orgTenantId,
            MembershipId = membershipId,
            AttributeId = attributeId,
            AttributeOptionId = optionId,
            Value = value,
            Weight = weight
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    /// <summary>Updates the value, option, and weight of this assignment.</summary>
    public void UpdateValue(string? value, AttributeOptionId? optionId, int? weight)
    {
        Value = value;
        AttributeOptionId = optionId;
        Weight = weight;
    }
}

/// <summary>
///     Persistence contract for <see cref="AttributeAssignment" /> aggregates.
/// </summary>
public interface IAttributeAssignmentRepository : ICrudRepository<AttributeAssignment, AttributeAssignmentId>
{
    /// <summary>Returns all assignments for the given membership.</summary>
    Task<IReadOnlyList<AttributeAssignment>> GetByMembershipAsync(
        MembershipId membershipId,
        CancellationToken cancellationToken);

    /// <summary>Returns all assignments for the given attribute (across all members).</summary>
    Task<IReadOnlyList<AttributeAssignment>> GetByAttributeAsync(
        AttributeId attributeId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Finds an existing assignment for a (membership, attribute, option) triple.
    ///     <paramref name="optionId" /> may be <see langword="null" /> for Text/Number types.
    /// </summary>
    Task<AttributeAssignment?> GetByMembershipAttributeOptionAsync(
        MembershipId membershipId,
        AttributeId attributeId,
        AttributeOptionId? optionId,
        CancellationToken cancellationToken);

    /// <summary>Returns all assignments that belong to the given org tenant.</summary>
    Task<IReadOnlyList<AttributeAssignment>> GetByOrgAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken);
}
