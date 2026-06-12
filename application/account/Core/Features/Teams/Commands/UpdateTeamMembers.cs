using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Commands;

/// <summary>
///     Updates the members of a team in bulk (adds and removes memberships immediately).
/// </summary>
[PublicAPI]
public sealed record UpdateTeamMembersCommand : ICommand, IRequest<Result>
{
    [JsonIgnore]
    public TenantId TeamId { get; init; } = null!;

    public required string[] AddUserIds { get; init; } = [];

    public required string[] RemoveUserIds { get; init; } = [];
}

public sealed class UpdateTeamMembersValidator : AbstractValidator<UpdateTeamMembersCommand>
{
    public UpdateTeamMembersValidator()
    {
        RuleFor(x => x.AddUserIds).NotNull();
        RuleFor(x => x.RemoveUserIds).NotNull();
    }
}

public sealed class UpdateTeamMembersHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    IUserRepository userRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<UpdateTeamMembersCommand, Result>
{
    public async Task<Result> Handle(UpdateTeamMembersCommand command, CancellationToken cancellationToken)
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
        if (parentId is null)
        {
            return Result.Unauthorized("User is not associated with a tenant.");
        }

        var userId = executionContext.UserInfo.Id;

        if (executionContext.ActiveOrgId is not null)
        {
            var caller = await membershipRepository.GetByUserAndTenantAsync(userId, parentId, cancellationToken);
            if (caller is null || !caller.Accepted)
            {
                return Result.Forbidden("You are not a member of this organization.");
            }

            if (caller.Role == MembershipRole.Member)
            {
                return Result.Forbidden("Only organization owners and admins can manage team memberships.");
            }
        }

        var team = await tenantRepository.GetByIdUnfilteredAsync(command.TeamId, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
        {
            return Result.NotFound($"Team '{command.TeamId}' not found.");
        }

        if (team.ParentTenantId != parentId)
        {
            return Result.Forbidden("This team does not belong to your account.");
        }

        // 1. Process Removals
        foreach (var rUserIdStr in command.RemoveUserIds)
        {
            if (!UserId.TryParse(rUserIdStr, out var rUserId))
            {
                continue;
            }

            var membership = await membershipRepository.GetByUserAndTenantAsync(rUserId, team.Id, cancellationToken);
            if (membership is not null)
            {
                // Last-owner protection
                if (membership.Role == MembershipRole.Owner)
                {
                    var ownerCount = await membershipRepository.CountOwnersAsync(team.Id, cancellationToken);
                    if (ownerCount <= 1)
                    {
                        return Result.BadRequest("Cannot remove the last Owner of a team.");
                    }
                }

                membershipRepository.Remove(membership);
                events.CollectEvent(new MembershipRemoved(membership.Id, team.Id, membership.Role));
            }
        }

        // 2. Process Additions
        foreach (var aUserIdStr in command.AddUserIds)
        {
            if (!UserId.TryParse(aUserIdStr, out var aUserId))
            {
                continue;
            }

            var existing = await membershipRepository.GetByUserAndTenantAsync(aUserId, team.Id, cancellationToken);
            if (existing is not null)
            {
                continue; // Already a member of this team
            }

            var user = await userRepository.GetByIdUnfilteredAsync(aUserId, cancellationToken);
            if (user is null)
            {
                continue;
            }

            // Create immediately accepted membership for the team member
            var directMembership = Membership.CreateInvite(
                team.Id,
                user.Id,
                MembershipRole.Member,
                userId,
                "DIRECT_ADD_" + Guid.NewGuid().ToString("N")
            );
            directMembership.Accept(timeProvider.GetUtcNow());

            await membershipRepository.AddAsync(directMembership, cancellationToken);
            events.CollectEvent(new TeamMemberInvited(team.Id, parentId, directMembership.Id, MembershipRole.Member));
        }

        return Result.Success();
    }
}
