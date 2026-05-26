using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Commands;

/// <summary>
///     Creates a new <see cref="TenantKind.Team" /> tenant as a child of the caller's active
///     organization. The calling user automatically becomes the seed <see cref="MembershipRole.Owner" />.
///     Mirrors cal.com's <c>viewer.teams.create</c> tRPC procedure
///     (<see href="cal.com/packages/trpc/server/routers/viewer/teams/create.handler.ts" />).
/// </summary>
[PublicAPI]
public sealed record CreateTeamCommand : ICommand, IRequest<Result<TeamResponse>>
{
    public required string Name { get; init; }

    public string? Slug { get; init; }
}

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).WithMessage("Team name must be between 1 and 255 characters.");
        RuleFor(x => x.Slug).MaximumLength(255).WithMessage("Team slug must be at most 255 characters.");
    }
}

public sealed class CreateTeamHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<CreateTeamCommand, Result<TeamResponse>>
{
    public async Task<Result<TeamResponse>> Handle(CreateTeamCommand command, CancellationToken cancellationToken)
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

        if (executionContext.ActiveOrgId is not null)
        {
            // Org context: the caller must be an accepted Owner or Admin of the organization.
            var callerMembership = await membershipRepository.GetByUserAndTenantAsync(userId, parentId, cancellationToken);
            if (callerMembership is null || !callerMembership.Accepted)
            {
                return Result<TeamResponse>.Forbidden("You are not a member of this organization.");
            }

            if (callerMembership.Role == MembershipRole.Member)
            {
                return Result<TeamResponse>.Forbidden("Only organization owners and admins can create teams.");
            }
        }
        // Solo context: user owns the tenant, no membership check required.

        var parent = await tenantRepository.GetByIdUnfilteredAsync(parentId, cancellationToken);
        if (parent is null) return Result<TeamResponse>.NotFound($"Tenant '{parentId}' not found.");
        if (parent.Kind == TenantKind.Team)
        {
            return Result<TeamResponse>.BadRequest("Teams cannot be nested under another team.");
        }

        if (!string.IsNullOrWhiteSpace(command.Slug))
        {
            var existing = await tenantRepository.GetTeamBySlugInOrgAsync(parentId, command.Slug, cancellationToken);
            if (existing is not null)
            {
                return Result<TeamResponse>.BadRequest($"A team with slug '{command.Slug}' already exists in this organization.");
            }
        }

        var rolloutIndex = await tenantRepository.GetNextRolloutIndexUnfilteredAsync(cancellationToken);
        var team = Tenant.CreateTeam(parent, rolloutIndex);
        team.Update(command.Name);
        if (!string.IsNullOrWhiteSpace(command.Slug)) team.SetSlug(command.Slug);

        await tenantRepository.AddAsync(team, cancellationToken);

        var seedOwner = Membership.CreateSeedOwner(team.Id, userId);
        await membershipRepository.AddAsync(seedOwner, cancellationToken);

        events.CollectEvent(new TeamCreated(team.Id, parentId));

        return team.ToResponse(1);
    }
}
