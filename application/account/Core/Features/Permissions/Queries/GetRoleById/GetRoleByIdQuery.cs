using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Permissions.Queries.GetRoleById;

[PublicAPI]
[RequirePermission(PermissionResource.Role, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetRoleByIdQuery : IRequest<Result<RoleResponse>>
{
    public required RoleId RoleId { get; init; }
}

public sealed class GetRoleByIdHandler(
    IRoleRepository roleRepository,
    AccountDbContext accountDbContext,
    IExecutionContext executionContext
) : IRequestHandler<GetRoleByIdQuery, Result<RoleResponse>>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
            return Result<RoleResponse>.Forbidden("The custom roles feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var role = await roleRepository.GetByIdWithPermissionsAsync(query.RoleId, cancellationToken);
        if (role is null)
            return Result<RoleResponse>.NotFound($"Role '{query.RoleId}' not found.");

        // System roles are returned to any reader; custom roles must be scoped to the active org.
        if (!role.IsSystem && role.TenantId != orgId)
            return Result<RoleResponse>.Forbidden("You do not have access to this role.");

        var memberCount = role.IsSystem
            ? 0
            : await accountDbContext.Set<Membership>()
                .CountAsync(m => m.TenantId == orgId && m.CustomRoleId == role.Id, cancellationToken);

        return role.ToResponse(memberCount);
    }
}
