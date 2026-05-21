using System.Diagnostics.CodeAnalysis;

namespace Account.Features.Permissions.Domain;

/// <summary>
///     A strongly-typed (resource, action) permission pair.
///     <para>
///         Permission strings are formatted as <c>"{lowerCamelResource}.{lowercaseAction}"</c> to
///         match cal.com's <c>PermissionString</c> convention (e.g., <c>"eventType.create"</c>,
///         <c>"auditLog.manage"</c>). Conversion is achieved by lower-casing the first character of
///         the <see cref="PermissionResource" /> enum name and lower-casing the
///         <see cref="PermissionAction" /> enum name entirely.
///     </para>
///     <para>
///         In the database, <see cref="Resource" /> and <see cref="Action" /> are stored as separate
///         columns in the <c>role_permissions</c> join table using the enum member name (PascalCase)
///         via EF Core's <c>EnumToStringConverter</c>. The <see cref="ToString()" /> lowerCamelCase
///         format is used for the public API surface and logging only.
///     </para>
///     <para>
///         Architectural decision: <c>Permission</c> is a <c>sealed record</c> (class-based) rather
///         than <c>record struct</c> to satisfy EF Core's requirement that owned entities are reference
///         types so it can perform reference tracking during <c>OwnsMany</c> materialisation.
///     </para>
/// </summary>
public sealed record Permission(PermissionResource Resource, PermissionAction Action)
{
    /// <summary>
    ///     The complete catalog of every (resource, action) combination. Useful for seeding and UI display.
    /// </summary>
    public static readonly IReadOnlySet<Permission> All = BuildAll();

    /// <summary>
    ///     Returns the canonical cal.com-compatible permission string, e.g. <c>"eventType.create"</c>.
    ///     The resource is lowerCamelCase; the action is fully lowercase.
    /// </summary>
    public override string ToString()
    {
        return $"{ResourceToString(Resource)}.{Action.ToString().ToLowerInvariant()}";
    }

    /// <summary>
    ///     Parses a permission string in the format <c>"resource.action"</c> (lowerCamelCase resource,
    ///     lowercase action) into a <see cref="Permission" />.
    /// </summary>
    /// <exception cref="FormatException">Thrown if <paramref name="value" /> is not a valid permission string.</exception>
    public static Permission Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException($"'{value}' is not a valid permission string. Expected format: 'resource.action'.");
        }

        return result;
    }

    /// <summary>
    ///     Attempts to parse a permission string in the format <c>"resource.action"</c> into a
    ///     <see cref="Permission" />.
    /// </summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out Permission? result)
    {
        result = null;
        if (value is null) return false;

        var dotIndex = value.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == value.Length - 1) return false;

        var resourcePart = value[..dotIndex];
        var actionPart = value[(dotIndex + 1)..];

        // lowerCamelCase → PascalCase: capitalise first character only.
        var pascalResource = char.ToUpper(resourcePart[0]) + resourcePart[1..];

        if (!Enum.TryParse<PermissionResource>(pascalResource, false, out var resource)) return false;
        if (!Enum.TryParse<PermissionAction>(actionPart, true, out var action)) return false;

        result = new Permission(resource, action);
        return true;
    }

    private static HashSet<Permission> BuildAll()
    {
        var set = new HashSet<Permission>();
        foreach (var resource in Enum.GetValues<PermissionResource>())
        {
            foreach (var action in Enum.GetValues<PermissionAction>())
            {
                set.Add(new Permission(resource, action));
            }
        }

        return set;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Converts a <see cref="PermissionResource" /> enum value to lowerCamelCase string.
    ///     Single-word names (e.g., <c>Booking</c>) become lowercase (<c>"booking"</c>).
    ///     Multi-word PascalCase names (e.g., <c>EventType</c>) have only their first character
    ///     lowercased (<c>"eventType"</c>), preserving the inner capitalisation.
    /// </summary>
    private static string ResourceToString(PermissionResource resource)
    {
        var name = resource.ToString();
        return char.ToLower(name[0]) + name[1..];
    }
}
