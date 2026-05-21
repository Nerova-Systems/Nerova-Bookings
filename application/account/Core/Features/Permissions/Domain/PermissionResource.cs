using JetBrains.Annotations;

namespace Account.Features.Permissions.Domain;

/// <summary>
///     The resource (domain area) component of a <see cref="Permission" /> grant.
///     Mirrors the resource vocabulary used in the cal.com PBAC <c>PermissionString</c> model,
///     extended with Nerova-specific resources.
///     <para>
///         When serialised to a permission string (see <see cref="Permission.ToString()" />), each
///         member is rendered in lowerCamelCase (e.g., <c>EventType</c> → <c>"eventType"</c>,
///         <c>AuditLog</c> → <c>"auditLog"</c>).
///     </para>
/// </summary>
[PublicAPI]
public enum PermissionResource
{
    /// <summary>Booking event types configured by a user or team.</summary>
    EventType,

    /// <summary>Bookings made by end-users.</summary>
    Booking,

    /// <summary>Team tenants and their settings.</summary>
    Team,

    /// <summary>Organization tenants and their settings.</summary>
    Organization,

    /// <summary>API keys for programmatic access.</summary>
    ApiKey,

    /// <summary>Automated follow-up workflows (e.g., reminder emails).</summary>
    Workflow,

    /// <summary>Analytics and insights dashboards.</summary>
    Insights,

    /// <summary>Team or org members.</summary>
    Member,

    /// <summary>Custom PBAC roles defined at org level.</summary>
    Role,

    /// <summary>Custom attributes attached to users or bookings.</summary>
    Attribute,

    /// <summary>Availability schedules.</summary>
    Schedule,

    /// <summary>Audit log entries.</summary>
    AuditLog,

    /// <summary>Single Sign-On configuration.</summary>
    Sso,

    /// <summary>SMTP / outbound email configuration.</summary>
    Smtp,

    /// <summary>Billing settings and invoices.</summary>
    Billing,

    /// <summary>User accounts managed within a team or organization.</summary>
    User,

    /// <summary>Organization-level settings (SMTP, delegation credentials, etc.).</summary>
    OrgSettings
}
