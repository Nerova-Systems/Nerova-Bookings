namespace Account.Features.Permissions.Pipeline;

/// <summary>
///     Determines which tenant context is used when evaluating a permission declared via
///     <see cref="RequirePermissionAttribute" />.
///     <para>
///         <see cref="CurrentTenant" /> (default) is backward-compatible: the permission is checked
///         against <see cref="SharedKernel.ExecutionContext.IExecutionContext.TenantId" />, exactly as
///         it was before context injection was introduced.
///     </para>
///     <para>
///         Use <see cref="Team" /> or <see cref="Organization" /> on endpoints that operate within the
///         org hierarchy and must verify membership/permissions in the active team or org scope rather
///         than the user's own (leaf) tenant.
///     </para>
/// </summary>
public enum PermissionScope
{
    /// <summary>
    ///     Check the permission against the user's current tenant
    ///     (<see cref="SharedKernel.ExecutionContext.IExecutionContext.TenantId" />).
    ///     This is the default and is backward-compatible with all existing attributed requests.
    /// </summary>
    CurrentTenant,

    /// <summary>
    ///     Check the permission against the active team tenant
    ///     (<see cref="SharedKernel.ExecutionContext.IExecutionContext.ActiveTeamId" />).
    ///     Returns HTTP 403 if <c>ActiveTeamId</c> is <see langword="null" /> (i.e., no team scope).
    /// </summary>
    Team,

    /// <summary>
    ///     Check the permission against the active organization tenant
    ///     (<see cref="SharedKernel.ExecutionContext.IExecutionContext.ActiveOrgId" />).
    ///     Returns HTTP 403 if <c>ActiveOrgId</c> is <see langword="null" /> (i.e., no org scope).
    /// </summary>
    Organization
}
