namespace Account.Features.AuditLog.Domain;

/// <summary>
///     Categorises the action performed during an audited event.
///     Stored as a PascalCase string via EF Core's enum-to-string converter.
/// </summary>
public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    Invited,
    Accepted,
    Declined,
    Assigned,
    Revoked,
    Enabled,
    Disabled,
    Started,
    Ended,
    Exported,
    Imported,
    Approved,
    Rejected,
    Configured,
    Tested,
    KeyRotated
}
