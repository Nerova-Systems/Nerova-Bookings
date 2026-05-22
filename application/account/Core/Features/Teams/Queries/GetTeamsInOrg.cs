using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Queries;

/// <summary>Returns every <see cref="TenantKind.Team" /> tenant that is a direct child of the caller's active organization.</summary>
[PublicAPI]
public sealed record GetTeamsInOrgQuery : IRequest<Result<TeamResponse[]>>;

public sealed class GetTeamsInOrgHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetTeamsInOrgQuery, Result<TeamResponse[]>>
{
    public async Task<Result<TeamResponse[]>> Handle(GetTeamsInOrgQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierTeams.Key))
            return Result<TeamResponse[]>.Forbidden("The teams feature is not enabled for this organization.");
        if (executionContext.ActiveOrgId is null)
            return Result<TeamResponse[]>.Forbidden("An active organization is required to list teams.");
        if (executionContext.UserInfo.Id is null)
            return Result<TeamResponse[]>.Unauthorized("User is not authenticated.");

        var orgId = executionContext.ActiveOrgId;
        var caller = await membershipRepository.GetByUserAndTenantAsync(executionContext.UserInfo.Id, orgId, cancellationToken);
        if (caller is null || !caller.Accepted)
            return Result<TeamResponse[]>.Forbidden("You are not a member of this organization.");

        var teams = await tenantRepository.GetChildrenOfAsync(orgId, cancellationToken);

        var responses = new TeamResponse[teams.Length];
        for (var i = 0; i < teams.Length; i++)
        {
            var members = await membershipRepository.GetMembersOfTenantAsync(teams[i].Id, includePending: true, cancellationToken);
            responses[i] = teams[i].ToResponse(members.Length);
        }

        return responses;
    }
}
