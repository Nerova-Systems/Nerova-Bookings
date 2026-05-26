using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Commands;

/// <summary>
///     Soft-deletes a <see cref="TenantKind.Team" />. Owner of the parent organization only.
///     Memberships are not explicitly removed — they are tied to the soft-deleted Team and become invisible
///     via the team's query filter; cascade-delete fires when the team is permanently removed.
/// </summary>
[PublicAPI]
public sealed record DeleteTeamCommand(TenantId Id) : ICommand, IRequest<Result>;

public sealed class DeleteTeamHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<DeleteTeamCommand, Result>
{
    public async Task<Result> Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierTeams.Key))
        {
            return Result.Forbidden("The teams feature is not enabled for this organization.");
        }

        if (executionContext.UserInfo.Id is null)
        {
            return Result.Unauthorized("User is not authenticated.");
        }

        var parentId = executionContext.ActiveOrgId ?? executionContext.TenantId;
        if (parentId is null) return Result.Unauthorized("User is not associated with a tenant.");
        var userId = executionContext.UserInfo.Id;

        var team = await tenantRepository.GetByIdUnfilteredAsync(command.Id, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
        {
            return Result.NotFound($"Team '{command.Id}' not found.");
        }

        if (team.ParentTenantId != parentId)
        {
            return Result.Forbidden("This team does not belong to your account.");
        }

        if (executionContext.ActiveOrgId is not null)
        {
            var caller = await membershipRepository.GetByUserAndTenantAsync(userId, parentId, cancellationToken);
            if (caller is null || !caller.Accepted)
            {
                return Result.Forbidden("You are not a member of this organization.");
            }

            if (caller.Role != MembershipRole.Owner)
            {
                return Result.Forbidden("Only organization owners can delete teams.");
            }
        }
        // Solo context: owning the parent tenant implies full access.

        var members = await membershipRepository.GetMembersOfTenantAsync(team.Id, true, cancellationToken);
        tenantRepository.Remove(team);

        events.CollectEvent(new TeamDeleted(team.Id, parentId, members.Length));

        return Result.Success();
    }
}
