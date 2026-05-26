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
[RequirePermission(PermissionResource.Role, PermissionAction.Update)]
public sealed record UpdateRoleCommand : ICommand, IRequest<Result<RoleResponse>>
{
    [JsonIgnore] // Removes this property from the API contract
    public RoleId RoleId { get; init; } = null!;

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
        {
            return Result<RoleResponse>.Forbidden("The custom roles feature is not enabled for this organization.");
        }

        var tenantId = executionContext.ActiveOrgId ?? executionContext.TenantId;
        if (tenantId is null) return Result<RoleResponse>.Unauthorized("User is not associated with a tenant.");

        var role = await roleRepository.GetByIdWithPermissionsAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleResponse>.NotFound($"Role '{command.RoleId}' not found.");
        }

        if (role.IsSystem)
        {
            return Result<RoleResponse>.BadRequest("System roles cannot be modified.");
        }

        if (role.TenantId != tenantId)
        {
            return Result<RoleResponse>.Forbidden("You do not have access to this role.");
        }

        var permissions = new List<Permission>();
        foreach (var key in command.Permissions)
        {
            if (!Permission.TryParse(key, out var permission))
            {
                return Result<RoleResponse>.BadRequest($"Permission '{key}' is not a valid permission string.");
            }

            permissions.Add(permission);
        }

        // Enforce per-tenant name uniqueness when the name actually changes.
        if (!string.Equals(role.Name, command.Name, StringComparison.Ordinal))
        {
            var clash = await roleRepository.GetByNameAsync(tenantId, command.Name, cancellationToken);
            if (clash is not null && clash.Id != role.Id)
            {
                return Result<RoleResponse>.BadRequest($"A role named '{command.Name}' already exists in this organization.");
            }
        }

        role.Rename(command.Name, command.Description);
        role.ReplacePermissions(permissions);
        roleRepository.Update(role);

        var members = await membershipRepository.GetByCustomRoleIdAsync(role.Id, cancellationToken);
        var memberCount = members.Length;

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                tenantId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Role),
                nameof(AuditAction.Updated),
                role.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        events.CollectEvent(new RoleUpdated(role.Id, tenantId));

        return role.ToResponse(memberCount);
    }
}
