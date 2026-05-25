namespace Main.Features.Permissions.Domain;

/// <summary>
///     A strongly-typed <c>(resource, action)</c> permission pair scoped to the main SCS.
///     <para>
///         Permission strings are formatted as <c>"{lowerCamelResource}.{lowercaseAction}"</c> to
///         match cal.com's <c>PermissionString</c> convention (e.g., <c>"booking.cancel"</c>,
///         <c>"eventType.duplicate"</c>).
///     </para>
///     <para>
///         Main intentionally defines its own <see cref="PermissionResource" /> /
///         <see cref="PermissionAction" /> enums (rather than referencing the Account SCS) to keep
///         the two SCSes independently deployable. The Account SCS owns the canonical permission
///         catalogue for cross-cutting concerns (users, billing, audit log); main owns the
///         scheduling-domain permission catalogue.
///     </para>
/// </summary>
public sealed record Permission(PermissionResource Resource, PermissionAction Action)
{
    public override string ToString()
    {
        return $"{ResourceToString(Resource)}.{Action.ToString().ToLowerInvariant()}";
    }

    private static string ResourceToString(PermissionResource resource)
    {
        var name = resource.ToString();
        return char.ToLower(name[0]) + name[1..];
    }
}
