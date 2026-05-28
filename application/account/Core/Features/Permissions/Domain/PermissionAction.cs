using JetBrains.Annotations;

namespace Account.Features.Permissions.Domain;

/// <summary>
///     The action component of a <see cref="Permission" /> grant.
///     Mirrors the action vocabulary used in the cal.com PBAC <c>PermissionString</c> model.
///     <para>
///         When serialised to a permission string (see <see cref="Permission.ToString()" />), each
///         member is rendered in lowercase (e.g., <c>Create</c> → <c>"create"</c>).
///     </para>
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionAction
{
    /// <summary>Create a new resource instance.</summary>
    Create,

    /// <summary>Read / view an existing resource instance.</summary>
    Read,

    /// <summary>Modify an existing resource instance.</summary>
    Update,

    /// <summary>Remove a resource instance permanently.</summary>
    Delete,

    /// <summary>Full administrative control over a resource, including sub-actions.</summary>
    Manage,

    /// <summary>Invite another user to access a resource (e.g., invite to team).</summary>
    Invite,

    /// <summary>List / enumerate resource instances without necessarily reading each one in full.</summary>
    List,

    /// <summary>Take on the identity of a user for support and debugging purposes (with full audit trail).</summary>
    Impersonate
}
