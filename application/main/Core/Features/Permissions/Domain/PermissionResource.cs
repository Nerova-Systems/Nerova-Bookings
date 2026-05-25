using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Main.Features.Permissions.Domain;

/// <summary>
///     The resource (domain area) component of a <see cref="Permission" /> grant in the main SCS.
///     <para>
///         Mirrors the resource vocabulary used by the cal.com PBAC <c>PermissionString</c> model and
///         the parallel <c>Account.Features.Permissions.Domain.PermissionResource</c> enum, but is
///         intentionally a separate type to preserve the main↔account SCS isolation (main does not
///         reference the Account assembly).
///     </para>
///     <para>
///         When serialised to a permission string (see <see cref="Permission.ToString()" />), each
///         member is rendered in lowerCamelCase (e.g., <c>EventType</c> → <c>"eventType"</c>).
///     </para>
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionResource
{
    /// <summary>Bookings made by end-users.</summary>
    Booking,

    /// <summary>Booking event types configured by a user or team.</summary>
    EventType,

    /// <summary>Availability schedules.</summary>
    Schedule
}
