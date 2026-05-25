using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Main.Features.Permissions.Domain;

/// <summary>
///     The action component of a <see cref="Permission" /> grant in the main SCS.
///     <para>
///         Combines the cal.com PBAC CRUD vocabulary (<c>Create</c>, <c>Read</c>, <c>Update</c>,
///         <c>Delete</c>, <c>Manage</c>) with scheduling-domain-specific custom actions
///         (<c>Cancel</c>, <c>Reschedule</c>, <c>Reassign</c>, <c>Report</c>, <c>Duplicate</c>) that
///         do not have a clean CRUD mapping but are first-class operations in the product.
///     </para>
///     <para>
///         When serialised to a permission string (see <see cref="Permission.ToString()" />), each
///         member is rendered in lowercase (e.g., <c>Reschedule</c> → <c>"reschedule"</c>).
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

    /// <summary>Cancel an existing booking.</summary>
    Cancel,

    /// <summary>Reschedule (request or commit a new slot for) an existing booking.</summary>
    Reschedule,

    /// <summary>Reassign a booking to a different host.</summary>
    Reassign,

    /// <summary>Run reports / aggregate analytics over the resource (e.g., booking insights).</summary>
    Report,

    /// <summary>Duplicate an existing resource (e.g., event type clone).</summary>
    Duplicate
}
