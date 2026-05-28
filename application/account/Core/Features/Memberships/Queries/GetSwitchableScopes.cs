using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Account.Features.Memberships.Queries;

/// <summary>
///     Returns the Solo / Organization / Team scopes the current user can switch between, used by the
///     federated tenant switcher in the user menu and side menu drawer. Combines email-matched Solo
///     tenants (the existing multi-account scenario) with the user's team and organization memberships
///     so the UI can render the cal.com-style 3-tier switcher (Solo → Org → Team).
/// </summary>
[PublicAPI]
public sealed record GetSwitchableScopesQuery : IRequest<Result<GetSwitchableScopesResponse>>;

[PublicAPI]
public sealed record GetSwitchableScopesResponse(SwitchableScopeInfo[] Scopes);

[PublicAPI]
public sealed record SwitchableScopeInfo(
    TenantId TenantId,
    string? TenantName,
    string? LogoUrl,
    TenantKind Kind,
    TenantId? ParentOrgId,
    bool IsCurrent,
    bool IsPending
);

internal sealed class GetSwitchableScopesQueryHandler(
    IUserRepository userRepository,
    IMembershipRepository membershipRepository,
    ITenantRepository tenantRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetSwitchableScopesQuery, Result<GetSwitchableScopesResponse>>
{
    public async Task<Result<GetSwitchableScopesResponse>> Handle(GetSwitchableScopesQuery query, CancellationToken cancellationToken)
    {
        var email = executionContext.UserInfo.Email;
        var currentUserId = executionContext.UserInfo.Id;
        var currentTenantId = executionContext.UserInfo.TenantId;

        if (email is null || currentUserId is null)
        {
            return Result<GetSwitchableScopesResponse>.Unauthorized("User is not authenticated.");
        }

        // Solo tenants: every tenant where the user's email exists (mirrors GetTenantsForUser behavior).
        var users = await userRepository.GetUsersByEmailUnfilteredAsync(email, cancellationToken);
        var soloTenantIds = users.Select(u => u.TenantId).ToArray();

        // Team / Organization memberships for the current user.
        var memberships = await membershipRepository.GetMembershipsOfUserAsync(currentUserId, cancellationToken);
        var membershipTenantIds = memberships.Select(m => m.TenantId).ToArray();

        var allTenantIds = soloTenantIds.Concat(membershipTenantIds).Distinct().ToArray();
        var tenants = await tenantRepository.GetByIdsUnfilteredAsync(allTenantIds, cancellationToken);
        var tenantById = tenants.ToDictionary(t => t.Id);

        var scopes = new List<SwitchableScopeInfo>(allTenantIds.Length);

        // Solo scopes — driven by user rows so we can surface "invitation pending" via EmailConfirmed.
        foreach (var user in users)
        {
            if (!tenantById.TryGetValue(user.TenantId, out var tenant)) continue;
            if (tenant.Kind != TenantKind.Solo) continue;

            scopes.Add(new SwitchableScopeInfo(
                    tenant.Id,
                    tenant.Name,
                    tenant.Logo.Url,
                    tenant.Kind,
                    null,
                    tenant.Id == currentTenantId,
                    !user.EmailConfirmed
                )
            );
        }

        // Team / Org scopes — driven by memberships so we can surface unaccepted invites.
        foreach (var membership in memberships)
        {
            if (!tenantById.TryGetValue(membership.TenantId, out var tenant)) continue;
            if (tenant.Kind == TenantKind.Solo) continue;

            scopes.Add(new SwitchableScopeInfo(
                    tenant.Id,
                    tenant.Name,
                    tenant.Logo.Url,
                    tenant.Kind,
                    tenant.ParentTenantId,
                    tenant.Id == currentTenantId,
                    !membership.Accepted
                )
            );
        }

        return new GetSwitchableScopesResponse(scopes.ToArray());
    }
}
