using Account.Features.Permissions.Domain;

namespace Account.Features.Permissions.Pipeline;

/// <summary>
///     Marks a MediatR request (command or query) as requiring a specific permission.
///     Apply this attribute to the request class; multiple attributes may be stacked when more
///     than one permission is required (all must be held — AND semantics).
///     <para>
///         The <see cref="PermissionCheckBehavior{TRequest,TResponse}" /> reads these attributes via a
///         static cached reflection lookup and denies the request with HTTP 403 Forbidden if the
///         authenticated user lacks any of the required permissions.
///     </para>
///     <para>
///         The optional <paramref name="scope" /> parameter controls which tenant the permission is
///         evaluated against. Defaults to <see cref="PermissionScope.CurrentTenant" /> for
///         backward-compatibility with all existing attributed requests.
///     </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(
    PermissionResource resource,
    PermissionAction action,
    PermissionScope scope = PermissionScope.CurrentTenant) : Attribute
{
    /// <summary>The permission this attribute demands.</summary>
    public Permission Permission { get; } = new(resource, action);

    /// <summary>The tenant scope against which the permission is evaluated.</summary>
    public PermissionScope Scope { get; } = scope;
}
