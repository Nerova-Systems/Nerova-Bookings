using Main.Features.Permissions.Domain;

namespace Main.Features.Permissions.Pipeline;

/// <summary>
///     Marks a MediatR request (command or query) as requiring a specific permission.
///     Apply this attribute to the request class; multiple attributes may be stacked when more
///     than one permission is required (all must be held — AND semantics).
///     <para>
///         The <see cref="PermissionCheckBehavior{TRequest,TResponse}" /> reads these attributes via a
///         static cached reflection lookup and denies the request with HTTP 403 Forbidden if the
///         authenticated user lacks any of the required permissions.
///     </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(
    PermissionResource resource,
    PermissionAction action,
    PermissionScope scope = PermissionScope.CurrentTenant
) : Attribute
{
    public Permission Permission { get; } = new(resource, action);

    public PermissionScope Scope { get; } = scope;
}
