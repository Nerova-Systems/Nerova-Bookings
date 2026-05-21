using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Permissions.Queries.GetAllPermissions;

[PublicAPI]
[RequirePermission(PermissionResource.Role, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetAllPermissionsQuery : IRequest<Result<PermissionGroupResponse[]>>;

public sealed class GetAllPermissionsHandler(
    IExecutionContext executionContext
) : IRequestHandler<GetAllPermissionsQuery, Result<PermissionGroupResponse[]>>
{
    public Task<Result<PermissionGroupResponse[]>> Handle(GetAllPermissionsQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
            return Task.FromResult(Result<PermissionGroupResponse[]>.Forbidden("The custom roles feature is not enabled for this organization."));

        var groups = Permission.All
            .GroupBy(p => p.Resource)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionGroupResponse(
                Resource: g.Key,
                Permissions: g.OrderBy(p => p.Action).Select(p => p.ToResponse()).ToArray()
            ))
            .ToArray();

        return Task.FromResult(Result<PermissionGroupResponse[]>.Success(groups));
    }
}
