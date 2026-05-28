using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Queries;

/// <summary>Returns details about a single <see cref="TenantKind.Team" /> the caller has access to.</summary>
[PublicAPI]
public sealed record GetTeamByIdQuery(TenantId Id) : IRequest<Result<TeamResponse>>;

public sealed class GetTeamByIdHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetTeamByIdQuery, Result<TeamResponse>>
{
    public async Task<Result<TeamResponse>> Handle(GetTeamByIdQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierTeams.Key))
        {
            return Result<TeamResponse>.Forbidden("The teams feature is not enabled for this organization.");
        }

        if (executionContext.UserInfo.Id is null)
        {
            return Result<TeamResponse>.Unauthorized("User is not authenticated.");
        }

        var parentId = executionContext.ActiveOrgId ?? executionContext.TenantId;
        if (parentId is null) return Result<TeamResponse>.Unauthorized("User is not associated with a tenant.");
        var userId = executionContext.UserInfo.Id;

        var team = await tenantRepository.GetByIdUnfilteredAsync(query.Id, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
        {
            return Result<TeamResponse>.NotFound($"Team '{query.Id}' not found.");
        }

        if (team.ParentTenantId != parentId)
        {
            return Result<TeamResponse>.Forbidden("This team does not belong to your account.");
        }

        if (executionContext.ActiveOrgId is not null)
        {
            // Org context: caller must be an org admin OR a direct team member.
            var orgMembership = await membershipRepository.GetByUserAndTenantAsync(userId, parentId, cancellationToken);
            var teamMembership = await membershipRepository.GetByUserAndTenantAsync(userId, team.Id, cancellationToken);

            var isOrgAdmin = orgMembership is { Accepted: true } and ({ Role: MembershipRole.Owner } or { Role: MembershipRole.Admin });
            var isTeamMember = teamMembership is { Accepted: true };
            if (!isOrgAdmin && !isTeamMember)
            {
                return Result<TeamResponse>.Forbidden("You do not have access to this team.");
            }
        }
        // Solo context: owning the parent tenant implies full access.

        var members = await membershipRepository.GetMembersOfTenantAsync(team.Id, true, cancellationToken);
        return team.ToResponse(members.Length);
    }
}
