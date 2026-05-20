using System.Net;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Validation;

namespace Account.Features.Permissions.Pipeline;

/// <summary>
///     MediatR pipeline behavior that enforces PBAC permission checks before the request reaches the
///     handler or the validation behavior.
///     <para>
///         When the request type is decorated with one or more <see cref="RequirePermissionAttribute" />
///         attributes the behavior:
///         <list type="number">
///             <item>Reads the current user's ID and tenant from <see cref="IExecutionContext" />.</item>
///             <item>Fails closed (HTTP 403) if the request is unauthenticated (null userId or tenantId).</item>
///             <item>
///                 Resolves the tenant to check against based on the attribute's <see cref="PermissionScope" />:
///                 <see cref="PermissionScope.CurrentTenant" /> uses <see cref="IExecutionContext.TenantId" />;
///                 <see cref="PermissionScope.Team" /> uses <see cref="IExecutionContext.ActiveTeamId" />;
///                 <see cref="PermissionScope.Organization" /> uses <see cref="IExecutionContext.ActiveOrgId" />.
///                 Returns HTTP 403 if the required scope tenant is null (e.g., no active team or org).
///             </item>
///             <item>
///                 Calls <see cref="IPermissionCheckService.HasPermissionAsync" /> for each required
///                 permission. Returns HTTP 403 on the first denial (fail-fast, AND semantics).
///             </item>
///         </list>
///         Requests with no <see cref="RequirePermissionAttribute" /> pass through untouched.
///     </para>
///     <para>
///         The set of required permissions is computed once per generic instantiation via a
///         <see langword="static" /> field, so reflection overhead is paid only at first use.
///     </para>
/// </summary>
public sealed class PermissionCheckBehavior<TRequest, TResponse>(
    IPermissionCheckService permissionCheckService,
    IExecutionContext executionContext,
    ILogger<PermissionCheckBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : ResultBase
{
    // Static per-generic-instantiation: reflection runs once per TRequest type at application startup.
    private static readonly IReadOnlyList<(Permission Permission, PermissionScope Scope)> RequiredPermissions =
        typeof(TRequest).GetCustomAttributes<RequirePermissionAttribute>(false)
            .Select(a => (a.Permission, a.Scope))
            .ToList();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (RequiredPermissions.Count == 0) return await next(cancellationToken);

        var userId = executionContext.UserInfo.Id;

        if (userId is null)
        {
            logger.LogWarning("Permission check failed: unauthenticated request on {RequestType}", typeof(TRequest).Name);
            return CreateForbiddenResult();
        }

        foreach (var (permission, scope) in RequiredPermissions)
        {
            var tenantId = ResolveScope(scope);
            if (tenantId is null)
            {
                logger.LogWarning(
                    "Permission check failed: scope {Scope} required but not set on {RequestType}",
                    scope, typeof(TRequest).Name
                );
                return CreateForbiddenResult();
            }

            if (!await permissionCheckService.HasPermissionAsync(userId, tenantId, permission, cancellationToken))
            {
                logger.LogWarning("Permission {Permission} denied for user {UserId} on tenant {TenantId}", permission, userId, tenantId);
                return CreateForbiddenResult();
            }
        }

        return await next(cancellationToken);
    }

    private TenantId? ResolveScope(PermissionScope scope)
    {
        return scope switch
        {
            PermissionScope.Team => executionContext.ActiveTeamId,
            PermissionScope.Organization => executionContext.ActiveOrgId,
            _ => executionContext.TenantId
        };
    }

    private static TResponse CreateForbiddenResult()
    {
        return (TResponse)Activator.CreateInstance(typeof(TResponse), HttpStatusCode.Forbidden, new ErrorMessage("You do not have permission to perform this action."), false, Array.Empty<ErrorDetail>(), null)!;
    }
}
