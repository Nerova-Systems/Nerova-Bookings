using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Memberships.Commands;

/// <summary>
///     Removes a <see cref="Membership" /> from a team or organization. Used by Owners/Admins
///     to remove a teammate, and by any user to leave a team they belong to.
///     Last-owner protection: cannot remove the only Owner of a tenant.
/// </summary>
[PublicAPI]
public sealed record RemoveMembershipCommand(MembershipId Id) : ICommand, IRequest<Result>;

public sealed class RemoveMembershipHandler(
    IMembershipRepository membershipRepository,
    ITenantRepository tenantRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<RemoveMembershipCommand, Result>
{
    public async Task<Result> Handle(RemoveMembershipCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierTeams.Key))
            return Result.Forbidden("The teams feature is not enabled for this organization.");
        if (executionContext.ActiveOrgId is null)
            return Result.Forbidden("An active organization is required to remove memberships.");
        if (executionContext.UserInfo.Id is null)
            return Result.Unauthorized("User is not authenticated.");

        var orgId = executionContext.ActiveOrgId;
        var userId = executionContext.UserInfo.Id;

        var target = await membershipRepository.GetByIdAsync(command.Id, cancellationToken);
        if (target is null) return Result.NotFound($"Membership '{command.Id}' not found.");

        // The membership must belong to the active org, or to a team that is a child of the active org.
        if (target.TenantId != orgId)
        {
            var team = await tenantRepository.GetByIdUnfilteredAsync(target.TenantId, cancellationToken);
            if (team is null || team.Kind != TenantKind.Team || team.ParentTenantId != orgId)
                return Result.Forbidden("This membership does not belong to your organization.");
        }

        var caller = await membershipRepository.GetByUserAndTenantAsync(userId, orgId, cancellationToken);
        if (caller is null || !caller.Accepted)
            return Result.Forbidden("You are not a member of this organization.");

        // Self-leave is always allowed (subject to last-owner protection). Otherwise need Owner/Admin in org.
        var isSelf = target.UserId == userId;
        if (!isSelf && caller.Role == MembershipRole.Member)
            return Result.Forbidden("Only organization owners and admins can remove other members.");

        // Last-owner protection
        if (target.Role == MembershipRole.Owner)
        {
            var ownerCount = await membershipRepository.CountOwnersAsync(target.TenantId, cancellationToken);
            if (ownerCount <= 1)
                return Result.BadRequest("Cannot remove the last Owner of a tenant.");
        }

        membershipRepository.Remove(target);
        events.CollectEvent(new MembershipRemoved(target.Id, target.TenantId, target.Role));
        return Result.Success();
    }
}
