namespace Main.Features.Permissions.Pipeline;

/// <summary>
///     Determines which tenant context is used when evaluating a permission declared via
///     <see cref="RequirePermissionAttribute" />. Mirrors the equivalent enum in the Account SCS.
/// </summary>
public enum PermissionScope
{
    /// <summary>
    ///     Check the permission against the user's current tenant
    ///     (<see cref="SharedKernel.ExecutionContext.IExecutionContext.TenantId" />). Default.
    /// </summary>
    CurrentTenant,

    /// <summary>
    ///     Check the permission against the active team tenant
    ///     (<see cref="SharedKernel.ExecutionContext.IExecutionContext.ActiveTeamId" />).
    /// </summary>
    Team,

    /// <summary>
    ///     Check the permission against the active organization tenant
    ///     (<see cref="SharedKernel.ExecutionContext.IExecutionContext.ActiveOrgId" />).
    /// </summary>
    Organization
}
