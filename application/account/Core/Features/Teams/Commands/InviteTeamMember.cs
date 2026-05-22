using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
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
///     Creates a pending <see cref="Membership" /> in a <see cref="TenantKind.Team" /> for an
///     existing user identified by email. Caller must be Owner or Admin of the parent organization.
///     <para>
///         v1 limitation: only existing-user invites are supported. Inviting a non-registered email
///         requires the cal.com signup-invite flow (creates an <c>InvitedEmail</c> record and
///         dispatches a signup-with-invite-token email). This is deferred to a separate task; the
///         endpoint returns <c>400 Bad Request</c> with a clear message.
///     </para>
/// </summary>
[PublicAPI]
public sealed record InviteTeamMemberCommand : ICommand, IRequest<Result<MembershipId>>
{
    [JsonIgnore] // Removes from API contract
    public TenantId TeamId { get; init; } = null!;

    public required string Email { get; init; }

    public required MembershipRole Role { get; init; }

    public RoleId? CustomRoleId { get; init; }
}

public sealed class InviteTeamMemberValidator : AbstractValidator<InviteTeamMemberCommand>
{
    public InviteTeamMemberValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class InviteTeamMemberHandler(
    ITenantRepository tenantRepository,
    IMembershipRepository membershipRepository,
    IUserRepository userRepository,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<InviteTeamMemberCommand, Result<MembershipId>>
{
    public async Task<Result<MembershipId>> Handle(InviteTeamMemberCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierTeams.Key))
            return Result<MembershipId>.Forbidden("The teams feature is not enabled for this organization.");
        if (executionContext.ActiveOrgId is null)
            return Result<MembershipId>.Forbidden("An active organization is required to invite team members.");
        if (executionContext.UserInfo.Id is null)
            return Result<MembershipId>.Unauthorized("User is not authenticated.");

        var orgId = executionContext.ActiveOrgId;
        var inviterId = executionContext.UserInfo.Id;

        var caller = await membershipRepository.GetByUserAndTenantAsync(inviterId, orgId, cancellationToken);
        if (caller is null || !caller.Accepted)
            return Result<MembershipId>.Forbidden("You are not a member of this organization.");
        if (caller.Role == MembershipRole.Member)
            return Result<MembershipId>.Forbidden("Only organization owners and admins can invite team members.");

        var team = await tenantRepository.GetByIdUnfilteredAsync(command.TeamId, cancellationToken);
        if (team is null || team.Kind != TenantKind.Team)
            return Result<MembershipId>.NotFound($"Team '{command.TeamId}' not found.");
        if (team.ParentTenantId != orgId)
            return Result<MembershipId>.Forbidden("This team does not belong to your organization.");

        var user = await userRepository.GetUserByEmailUnfilteredAsync(command.Email, cancellationToken);
        if (user is null)
            return Result<MembershipId>.BadRequest(
                $"User with email '{command.Email}' does not have an account; the signup-invite flow is not yet implemented.");

        var existing = await membershipRepository.GetByUserAndTenantAsync(user.Id, team.Id, cancellationToken);
        if (existing is not null)
            return Result<MembershipId>.Conflict($"User with email '{command.Email}' already has a membership in this team.");

        var inviteToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invite = Membership.CreateInvite(team.Id, user.Id, command.Role, inviterId, inviteToken);
        if (command.CustomRoleId is not null) invite.AssignCustomRole(command.CustomRoleId);

        await membershipRepository.AddAsync(invite, cancellationToken);

        events.CollectEvent(new TeamMemberInvited(team.Id, orgId, invite.Id, command.Role));

        return invite.Id;
    }
}
