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
            return Result<TeamResponse>.Forbidden("The teams feature is not enabled for this organization.");
        if (executionContext.ActiveOrgId is null)
            return Result<TeamResponse>.Forbidden("An active organization is required to read team details.");
        if (executionContext.UserInfo.Id is null)
            return Result<TeamResponse>.Unauthorized("User is not authenticated.");

        var orgId = executionContext.ActiveOrgId;
        var userId = executionContext.UserInfo.Id;

        var team = await tenantRepository.GetByIdUnfilteredAsync(query.Id, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
            return Result<TeamResponse>.NotFound($"Team '{query.Id}' not found.");
        if (team.ParentTenantId != orgId)
            return Result<TeamResponse>.Forbidden("This team does not belong to your organization.");

        // Authorization: caller must be either a member of the team OR an Owner/Admin of the parent org.
        var orgMembership = await membershipRepository.GetByUserAndTenantAsync(userId, orgId, cancellationToken);
        var teamMembership = await membershipRepository.GetByUserAndTenantAsync(userId, team.Id, cancellationToken);

        var isOrgAdmin = orgMembership is { Accepted: true } and ({ Role: MembershipRole.Owner } or { Role: MembershipRole.Admin });
        var isTeamMember = teamMembership is { Accepted: true };
        if (!isOrgAdmin && !isTeamMember)
            return Result<TeamResponse>.Forbidden("You do not have access to this team.");

        var members = await membershipRepository.GetMembersOfTenantAsync(team.Id, includePending: true, cancellationToken);
        return team.ToResponse(members.Length);
    }
}
