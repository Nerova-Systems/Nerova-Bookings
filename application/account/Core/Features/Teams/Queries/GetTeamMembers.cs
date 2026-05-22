using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Queries;

/// <summary>Returns the members of a team (including pending invites).</summary>
[PublicAPI]
public sealed record GetTeamMembersQuery(TenantId TeamId) : IRequest<Result<TeamMemberResponse[]>>;

public sealed class GetTeamMembersHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetTeamMembersQuery, Result<TeamMemberResponse[]>>
{
    public async Task<Result<TeamMemberResponse[]>> Handle(GetTeamMembersQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierTeams.Key))
            return Result<TeamMemberResponse[]>.Forbidden("The teams feature is not enabled for this organization.");
        if (executionContext.ActiveOrgId is null)
            return Result<TeamMemberResponse[]>.Forbidden("An active organization is required to read team members.");
        if (executionContext.UserInfo.Id is null)
            return Result<TeamMemberResponse[]>.Unauthorized("User is not authenticated.");

        var orgId = executionContext.ActiveOrgId;
        var userId = executionContext.UserInfo.Id;

        var team = await tenantRepository.GetByIdUnfilteredAsync(query.TeamId, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
            return Result<TeamMemberResponse[]>.NotFound($"Team '{query.TeamId}' not found.");
        if (team.ParentTenantId != orgId)
            return Result<TeamMemberResponse[]>.Forbidden("This team does not belong to your organization.");

        var orgMembership = await membershipRepository.GetByUserAndTenantAsync(userId, orgId, cancellationToken);
        var teamMembership = await membershipRepository.GetByUserAndTenantAsync(userId, team.Id, cancellationToken);
        var isOrgAdmin = orgMembership is { Accepted: true } and ({ Role: MembershipRole.Owner } or { Role: MembershipRole.Admin });
        var isTeamMember = teamMembership is { Accepted: true };
        if (!isOrgAdmin && !isTeamMember)
            return Result<TeamMemberResponse[]>.Forbidden("You do not have access to this team.");

        var memberships = await membershipRepository.GetMembersOfTenantAsync(team.Id, includePending: true, cancellationToken);
        if (memberships.Length == 0) return Array.Empty<TeamMemberResponse>();

        var users = await userRepository.GetByIdsAsync(memberships.Select(m => m.UserId).Distinct().ToArray(), cancellationToken);
        var usersById = users.ToDictionary(u => u.Id);

        return memberships
            .Select(m =>
            {
                usersById.TryGetValue(m.UserId, out var u);
                return new TeamMemberResponse(
                    MembershipId: m.Id,
                    UserId: m.UserId,
                    Email: u?.Email ?? string.Empty,
                    FirstName: u?.FirstName,
                    LastName: u?.LastName,
                    AvatarUrl: u?.Avatar.Url,
                    Role: m.Role,
                    CustomRoleId: m.CustomRoleId,
                    Accepted: m.Accepted,
                    AcceptedAt: m.AcceptedAt,
                    InvitedAt: m.CreatedAt);
            })
            .ToArray();
    }
}
