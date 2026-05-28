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

namespace Account.Features.Permissions.Commands.DeleteRole;

[PublicAPI]
[RequirePermission(PermissionResource.Role, PermissionAction.Delete)]
public sealed record DeleteRoleCommand : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes this property from the API contract
    public RoleId RoleId { get; init; } = null!;
}

public sealed class DeleteRoleHandler(
    IRoleRepository roleRepository,
    IMembershipRepository membershipRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
        {
            return Result.Forbidden("The custom roles feature is not enabled for this organization.");
        }

        var tenantId = executionContext.ActiveOrgId ?? executionContext.TenantId;
        if (tenantId is null) return Result.Unauthorized("User is not associated with a tenant.");

        var role = await roleRepository.GetByIdWithPermissionsAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.NotFound($"Role '{command.RoleId}' not found.");
        }

        if (role.IsSystem)
        {
            return Result.BadRequest("System roles cannot be deleted.");
        }

        if (role.TenantId != tenantId)
        {
            return Result.Forbidden("You do not have access to this role.");
        }

        // Cascade-unassign the custom role from every membership that references it. The custom role
        // override is then cleared, reverting affected members to the permissions of their system role.
        var assignedMembers = await membershipRepository.GetByCustomRoleIdAsync(role.Id, cancellationToken);
        foreach (var member in assignedMembers)
        {
            member.ClearCustomRole();
            membershipRepository.Update(member);
        }

        roleRepository.Remove(role);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                tenantId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Role),
                nameof(AuditAction.Deleted),
                role.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        events.CollectEvent(new RoleDeleted(role.Id, tenantId, assignedMembers.Length));

        return Result.Success();
    }
}
