namespace Account.Features.AuditLog.Domain;

/// <summary>
///     Categorises the resource type affected during an audited event.
///     Stored as a PascalCase string via EF Core's enum-to-string converter.
/// </summary>
public enum AuditResource
{
    Membership,
    Role,
    Tenant,
    Booking,
    EventType,
    User,
    ApiKey,
    Workflow,
    Insights,
    Attribute,
    Schedule,
    Sso,
    Smtp,
    Billing,
    OrgProfile,
    DelegationCredential
}
