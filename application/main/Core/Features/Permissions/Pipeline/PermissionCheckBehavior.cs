using System.Net;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Services;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Validation;

namespace Main.Features.Permissions.Pipeline;

/// <summary>
///     MediatR pipeline behaviour that enforces PBAC permission checks before the request reaches
///     the handler or the validation behaviour. Mirrors the Account SCS implementation.
///     <para>
///         Requests with no <see cref="RequirePermissionAttribute" /> pass through untouched. The set
///         of required permissions is computed once per generic instantiation via a
///         <see langword="static" /> field, so reflection overhead is paid only at first use.
///     </para>
/// </summary>
public sealed class PermissionCheckBehavior<TRequest, TResponse>(
    IPermissionCheckService permissionCheckService,
    IExecutionContext executionContext,
    ILogger<PermissionCheckBehavior<TRequest, TResponse>> logger
)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : ResultBase
{
    private static readonly IReadOnlyList<(Permission Permission, PermissionScope Scope)> RequiredPermissions =
        typeof(TRequest).GetCustomAttributes<RequirePermissionAttribute>(false)
            .Select(a => (a.Permission, a.Scope))
            .ToList();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (RequiredPermissions.Count == 0) return await next(cancellationToken);

        var userInfo = executionContext.UserInfo;

        if (userInfo.Id is null)
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

            if (!permissionCheckService.HasPermission(userInfo, tenantId, permission))
            {
                logger.LogWarning("Permission {Permission} denied for user {UserId} on tenant {TenantId}", permission, userInfo.Id, tenantId);
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
        return (TResponse)Activator.CreateInstance(
            typeof(TResponse),
            HttpStatusCode.Forbidden,
            new ErrorMessage("You do not have permission to perform this action."),
            false,
            Array.Empty<ErrorDetail>(),
            null
        )!;
    }
}
