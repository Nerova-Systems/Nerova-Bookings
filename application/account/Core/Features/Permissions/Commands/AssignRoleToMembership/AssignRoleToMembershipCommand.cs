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
[RequirePermission(PermissionResource.Member, PermissionAction.Update)]
public sealed record AssignRoleToMembershipCommand : ICommand, IRequest<Result>
{
    public required MembershipId MembershipId { get; init; }

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
        {
            return Result.Forbidden("The custom roles feature is not enabled for this organization.");
        }

        var tenantId = executionContext.ActiveOrgId ?? executionContext.TenantId;
        if (tenantId is null) return Result.Unauthorized("User is not associated with a tenant.");

        var membership = await membershipRepository.GetByIdAsync(command.MembershipId, cancellationToken);
        if (membership is null)
        {
            return Result.NotFound($"Membership '{command.MembershipId}' not found.");
        }

        if (membership.TenantId != tenantId)
        {
            return Result.Forbidden("You do not have access to this membership.");
        }

        if (command.RoleId is null)
        {
            membership.ClearCustomRole();
        }
        else
        {
            var role = await roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
            if (role is null)
            {
                return Result.NotFound($"Role '{command.RoleId}' not found.");
            }

            if (role.IsSystem)
            {
                return Result.BadRequest("System roles cannot be assigned as a custom role override.");
            }

            if (role.TenantId != tenantId)
            {
                return Result.Forbidden("This role is not available in the current organization.");
            }

            membership.AssignCustomRole(role.Id);
        }

        membershipRepository.Update(membership);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                tenantId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Membership),
                command.RoleId is null ? nameof(AuditAction.Revoked) : nameof(AuditAction.Assigned),
                membership.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        events.CollectEvent(new MembershipRoleAssigned(membership.Id, command.RoleId, tenantId));

        return Result.Success();
    }
}
