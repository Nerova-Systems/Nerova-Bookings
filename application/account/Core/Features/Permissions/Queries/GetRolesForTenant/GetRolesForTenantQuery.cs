using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Permissions.Queries.GetRolesForTenant;

[PublicAPI]
[RequirePermission(PermissionResource.Role, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetRolesForTenantQuery : IRequest<Result<RoleResponse[]>>;

public sealed class GetRolesForTenantHandler(
    IRoleRepository roleRepository,
    AccountDbContext accountDbContext,
    IExecutionContext executionContext
) : IRequestHandler<GetRolesForTenantQuery, Result<RoleResponse[]>>
{
    public async Task<Result<RoleResponse[]>> Handle(GetRolesForTenantQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
            return Result<RoleResponse[]>.Forbidden("The custom roles feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var systemRoles = await roleRepository.GetSystemRolesAsync(cancellationToken);
        var customRoles = await roleRepository.GetCustomRolesForTenantAsync(orgId, cancellationToken);

        // Batch member counts for this org by custom role. System roles have no count (Membership.Role
        // already governs the default scoping); they always render with MemberCount = 0.
        var customRoleIds = customRoles.Select(r => r.Id).ToArray();
        Dictionary<RoleId, int> countsByRole;
        if (customRoleIds.Length == 0)
        {
            countsByRole = new Dictionary<RoleId, int>();
        }
        else
        {
            countsByRole = await accountDbContext.Set<Membership>()
                .Where(m => m.TenantId == orgId && m.CustomRoleId != null && customRoleIds.Contains(m.CustomRoleId!))
                .GroupBy(m => m.CustomRoleId!)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);
        }

        var systemResponses = systemRoles
            .OrderBy(r => r.Name)
            .Select(r => r.ToResponse(memberCount: 0));

        var customResponses = customRoles
            .OrderBy(r => r.Name)
            .Select(r => r.ToResponse(countsByRole.TryGetValue(r.Id, out var c) ? c : 0));

        return systemResponses.Concat(customResponses).ToArray();
    }
}
