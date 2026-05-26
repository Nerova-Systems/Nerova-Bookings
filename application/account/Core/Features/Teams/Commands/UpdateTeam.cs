using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Teams.Commands;

/// <summary>
///     Updates the name, slug, and branding metadata of an existing
///     <see cref="TenantKind.Team" />. Caller must be an Owner or Admin of the parent organization.
/// </summary>
[PublicAPI]
public sealed record UpdateTeamCommand : ICommand, IRequest<Result<TeamResponse>>
{
    [JsonIgnore] // Removes from API contract
    public TenantId Id { get; init; } = null!;

    public required string Name { get; init; }

    public string? Slug { get; init; }

    public string? Bio { get; init; }

    public bool HideBranding { get; init; }

    public bool HideTeamProfileLink { get; init; }

    public bool IsPrivate { get; init; }

    public bool HideBookATeamMember { get; init; }

    public string? Theme { get; init; }

    public string? BrandColor { get; init; }

    public string? DarkBrandColor { get; init; }

    public int? TimeFormat { get; init; }

    public string? TimeZone { get; init; }

    public string? WeekStart { get; init; }
}

public sealed class UpdateTeamValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).WithMessage("Team name must be between 1 and 255 characters.");
        RuleFor(x => x.Slug).MaximumLength(255);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.TimeFormat).Must(tf => tf is null or 12 or 24).WithMessage("Time format must be 12 or 24.");
    }
}

public sealed class UpdateTeamHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<UpdateTeamCommand, Result<TeamResponse>>
{
    public async Task<Result<TeamResponse>> Handle(UpdateTeamCommand command, CancellationToken cancellationToken)
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
            var caller = await membershipRepository.GetByUserAndTenantAsync(userId, parentId, cancellationToken);
            if (caller is null || !caller.Accepted)
            {
                return Result<TeamResponse>.Forbidden("You are not a member of this organization.");
            }

            if (caller.Role == MembershipRole.Member)
            {
                return Result<TeamResponse>.Forbidden("Only organization owners and admins can update teams.");
            }
        }
        // Solo context: owning the parent tenant implies full access.

        var team = await tenantRepository.GetByIdUnfilteredAsync(command.Id, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
        {
            return Result<TeamResponse>.NotFound($"Team '{command.Id}' not found.");
        }

        if (team.ParentTenantId != parentId)
        {
            return Result<TeamResponse>.Forbidden("This team does not belong to your account.");
        }

        if (!string.IsNullOrWhiteSpace(command.Slug) && command.Slug != team.Slug)
        {
            var existing = await tenantRepository.GetTeamBySlugInOrgAsync(parentId, command.Slug, cancellationToken);
            if (existing is not null && existing.Id != team.Id)
            {
                return Result<TeamResponse>.BadRequest($"A team with slug '{command.Slug}' already exists in this organization.");
            }
        }

        team.Update(command.Name);
        team.SetSlug(string.IsNullOrWhiteSpace(command.Slug) ? null : command.Slug);
        team.UpdateBranding(
            command.Bio,
            command.HideBranding,
            command.HideTeamProfileLink,
            command.IsPrivate,
            command.HideBookATeamMember,
            command.Theme,
            command.BrandColor,
            command.DarkBrandColor,
            command.TimeFormat,
            command.TimeZone,
            command.WeekStart
        );

        tenantRepository.Update(team);

        var members = await membershipRepository.GetMembersOfTenantAsync(team.Id, true, cancellationToken);
        events.CollectEvent(new TeamUpdated(team.Id, parentId));

        return team.ToResponse(members.Length);
    }
}
