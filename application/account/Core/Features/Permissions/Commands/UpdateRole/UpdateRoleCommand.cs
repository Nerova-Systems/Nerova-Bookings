using Account.Features.AuditLog.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Permissions.Commands.UpdateRole;

[PublicAPI]
[RequirePermission(PermissionResource.Role, PermissionAction.Update, PermissionScope.Organization)]
public sealed record UpdateRoleCommand : ICommand, IRequest<Result<RoleResponse>>
{
    public RoleId RoleId { get; init; } = default!;

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string[] Permissions { get; init; } = [];
}

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Role name must be between 1 and 255 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .WithMessage("Role description cannot exceed 1024 characters.");
    }
}

public sealed class UpdateRoleHandler(
    IRoleRepository roleRepository,
    IMembershipRepository membershipRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<UpdateRoleCommand, Result<RoleResponse>>
{
    public async Task<Result<RoleResponse>> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
            return Result<RoleResponse>.Forbidden("The custom roles feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var role = await roleRepository.GetByIdWithPermissionsAsync(command.RoleId, cancellationToken);
        if (role is null)
            return Result<RoleResponse>.NotFound($"Role '{command.RoleId}' not found.");

        if (role.IsSystem)
            return Result<RoleResponse>.BadRequest("System roles cannot be modified.");

        if (role.TenantId != orgId)
            return Result<RoleResponse>.Forbidden("You do not have access to this role.");

        var permissions = new List<Permission>();
        foreach (var key in command.Permissions)
        {
            if (!Permission.TryParse(key, out var permission))
                return Result<RoleResponse>.BadRequest($"Permission '{key}' is not a valid permission string.");

            permissions.Add(permission);
        }

        // Enforce per-tenant name uniqueness when the name actually changes.
        if (!string.Equals(role.Name, command.Name, StringComparison.Ordinal))
        {
            var clash = await roleRepository.GetByNameAsync(orgId, command.Name, cancellationToken);
            if (clash is not null && clash.Id != role.Id)
                return Result<RoleResponse>.BadRequest($"A role named '{command.Name}' already exists in this organization.");
        }

        role.Rename(command.Name, command.Description);
        role.ReplacePermissions(permissions);
        roleRepository.Update(role);

        var members = await membershipRepository.GetByCustomRoleIdAsync(role.Id, cancellationToken);
        var memberCount = members.Length;

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.Role.ToString(),
            Action: AuditAction.Updated.ToString(),
            ResourceId: role.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        events.CollectEvent(new RoleUpdated(role.Id, orgId));

        return role.ToResponse(memberCount);
    }
}
