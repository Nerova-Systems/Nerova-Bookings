using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Permissions.Domain;

/// <summary>
///     Strongly-typed identifier for a <see cref="Role" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>rol</c>.
///     System roles use deterministic IDs defined in <see cref="SystemRoles" />.
/// </summary>
[PublicAPI]
[IdPrefix("rol")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, RoleId>))]
public sealed record RoleId(string Value) : StronglyTypedUlid<RoleId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A named bundle of <see cref="Permission" /> grants scoped to a <see cref="TenantId" /> (custom
///     role) or available to all tenants (system role).
///     <para>
///         System roles (<c>Owner</c>, <c>Admin</c>, <c>Member</c>) have <see cref="TenantId" />
///         <see langword="null" /> and are seeded via migration. They cannot be mutated or deleted.
///     </para>
///     <para>
///         Custom roles have a non-null <see cref="TenantId" /> pointing to a
///         <see cref="TenantKind.Team" /> or <see cref="TenantKind.Organization" /> tenant.
///         They can be granted, revoked, and renamed by org admins.
///     </para>
///     <para>
///         Mirrors the <c>Role</c> model in the cal.com Prisma schema
///         (<see href="https://github.com/calcom/cal.com/blob/main/packages/prisma/schema.prisma" />).
///     </para>
/// </summary>
public sealed class Role : AggregateRoot<RoleId>
{
    // List<T> is used (not HashSet<T>) so that EF Core can populate the collection via the
    // backing field during OwnsMany materialisation. EF 8 change-tracking wraps the field
    // directly; HashSet does not have a stable EF-supported binding when property type is
    // IReadOnlyCollection. Uniqueness is enforced in Grant() and by the composite PK in the DB.
    private readonly List<Permission> _permissions = [];

    private Role(RoleId id, TenantId? tenantId, string name, string? description) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
    }

    /// <summary>
    ///     The organisation or team this role is scoped to. <see langword="null" /> for system roles
    ///     that are available to every tenant. Maps to cal.com <c>Role.teamId</c>.
    /// </summary>
    public TenantId? TenantId { get; }

    /// <summary>Human-readable name, e.g. <c>"Owner"</c>, <c>"Editor"</c>.</summary>
    public string Name { get; private set; }

    /// <summary>Optional free-text description shown in the org settings UI.</summary>
    public string? Description { get; private set; }

    /// <summary>
    ///     <see langword="true" /> when this role is a Nerova system role (Owner, Admin, Member)
    ///     that applies to all tenants. System roles cannot be mutated or deleted.
    /// </summary>
    public bool IsSystem => TenantId is null;

    /// <summary>
    ///     The set of permissions granted by this role. Persisted in the <c>role_permissions</c> join
    ///     table. Mutate via <see cref="Grant" /> / <see cref="Revoke" />.
    /// </summary>
    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    // ─── Factory methods ──────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a system role for DB seeding. The caller supplies a deterministic
    ///     <paramref name="id" /> from <see cref="SystemRoles" /> so that subsequent migrations and
    ///     lookups can reference it by a known constant.
    /// </summary>
    public static Role CreateSystem(RoleId id, string name, string? description, IEnumerable<Permission> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var role = new Role(id, null, name, description);
        foreach (var p in permissions)
        {
            // Always create a fresh instance — owned entity tracking is reference-based.
            // Passing a shared instance (e.g. from Permission.All) across multiple roles
            // would cause EF Core to re-assign the object to the last role that "claims" it.
            if (!role._permissions.Any(x => x.Resource == p.Resource && x.Action == p.Action))
            {
                role._permissions.Add(new Permission(p.Resource, p.Action));
            }
        }

        return role;
    }

    /// <summary>
    ///     Creates a custom role scoped to the given <paramref name="tenantId" />.
    ///     The tenant must be a <see cref="TenantKind.Team" /> or
    ///     <see cref="TenantKind.Organization" /> — Solo tenants cannot own custom roles.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="tenantKind" /> is <see cref="TenantKind.Solo" />.
    /// </exception>
    public static Role CreateCustom(TenantId tenantId, TenantKind tenantKind, string name, string? description, IEnumerable<Permission>? initialPermissions = null)
    {
        if (tenantKind == TenantKind.Solo)
        {
            throw new InvalidOperationException("Solo tenants cannot own custom roles. Only Team or Organization tenants may create custom roles.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var role = new Role(RoleId.NewId(), tenantId, name, description);
        if (initialPermissions is not null)
        {
            foreach (var p in initialPermissions)
            {
                if (!role._permissions.Any(x => x.Resource == p.Resource && x.Action == p.Action))
                {
                    role._permissions.Add(new Permission(p.Resource, p.Action));
                }
            }
        }

        return role;
    }

    // ─── Mutation methods ─────────────────────────────────────────────────────

    /// <summary>
    ///     Adds a <see cref="Permission" /> to this role. Duplicate grants are ignored.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is a system role.</exception>
    public void Grant(Permission permission)
    {
        ThrowIfSystem();
        if (!_permissions.Any(x => x.Resource == permission.Resource && x.Action == permission.Action))
        {
            _permissions.Add(new Permission(permission.Resource, permission.Action));
        }
    }

    /// <summary>
    ///     Removes a <see cref="Permission" /> from this role. Removing a non-existent permission is a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is a system role.</exception>
    public void Revoke(Permission permission)
    {
        ThrowIfSystem();
        var existing = _permissions.FirstOrDefault(x => x.Resource == permission.Resource && x.Action == permission.Action);
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }
    }

    /// <summary>
    ///     Renames this custom role and updates its description.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is a system role.</exception>
    public void Rename(string newName, string? description)
    {
        ThrowIfSystem();
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        Name = newName;
        Description = description;
    }

    /// <summary>
    ///     Replaces this role's permission set with <paramref name="newPermissions" />, removing any
    ///     permissions not in the new set and adding any not currently granted. The collection is
    ///     mutated in place so EF Core's owned-entity change tracking picks up adds and removes.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is a system role.</exception>
    public void ReplacePermissions(IEnumerable<Permission> newPermissions)
    {
        ThrowIfSystem();
        ArgumentNullException.ThrowIfNull(newPermissions);

        var target = newPermissions.Distinct().ToArray();
        // Remove any current permission that is not in the target set.
        _permissions.RemoveAll(existing =>
            !target.Any(t => t.Resource == existing.Resource && t.Action == existing.Action));
        // Add any target permission that is not already present.
        foreach (var p in target)
        {
            if (!_permissions.Any(x => x.Resource == p.Resource && x.Action == p.Action))
            {
                _permissions.Add(new Permission(p.Resource, p.Action));
            }
        }
    }

    private void ThrowIfSystem()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("System roles cannot be modified. Create a custom role instead.");
        }
    }
}
