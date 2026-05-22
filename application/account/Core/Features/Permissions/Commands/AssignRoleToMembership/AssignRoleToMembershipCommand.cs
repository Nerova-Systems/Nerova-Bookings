using Account.Features.AuditLog.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Permissions.Commands.AssignRoleToMembership;

[PublicAPI]
[RequirePermission(PermissionResource.Member, PermissionAction.Update, PermissionScope.Organization)]
public sealed record AssignRoleToMembershipCommand : ICommand, IRequest<Result>
{
    public MembershipId MembershipId { get; init; } = default!;

    /// <summary>
    ///     The custom role to assign. When <see langword="null" />, the membership's custom role
    ///     override is cleared and the user reverts to the permissions of their system role.
    /// </summary>
    public RoleId? RoleId { get; init; }
}

public sealed class AssignRoleToMembershipHandler(
    IMembershipRepository membershipRepository,
    IRoleRepository roleRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<AssignRoleToMembershipCommand, Result>
{
    public async Task<Result> Handle(AssignRoleToMembershipCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
            return Result.Forbidden("The custom roles feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var membership = await membershipRepository.GetByIdAsync(command.MembershipId, cancellationToken);
        if (membership is null)
            return Result.NotFound($"Membership '{command.MembershipId}' not found.");

        if (membership.TenantId != orgId)
            return Result.Forbidden("You do not have access to this membership.");

        if (command.RoleId is null)
        {
            membership.ClearCustomRole();
        }
        else
        {
            var role = await roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
            if (role is null)
                return Result.NotFound($"Role '{command.RoleId}' not found.");

            if (role.IsSystem)
                return Result.BadRequest("System roles cannot be assigned as a custom role override.");

            if (role.TenantId != orgId)
                return Result.Forbidden("This role is not available in the current organization.");

            membership.AssignCustomRole(role.Id);
        }

        membershipRepository.Update(membership);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.Membership.ToString(),
            Action: command.RoleId is null ? AuditAction.Revoked.ToString() : AuditAction.Assigned.ToString(),
            ResourceId: membership.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        events.CollectEvent(new MembershipRoleAssigned(membership.Id, command.RoleId, orgId));

        return Result.Success();
    }
}
