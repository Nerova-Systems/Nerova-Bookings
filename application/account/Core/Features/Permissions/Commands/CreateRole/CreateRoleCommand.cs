using Account.Features.AuditLog.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Permissions.Commands.CreateRole;

[PublicAPI]
[RequirePermission(PermissionResource.Role, PermissionAction.Create, PermissionScope.Organization)]
public sealed record CreateRoleCommand : ICommand, IRequest<Result<RoleResponse>>
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string[] Permissions { get; init; } = [];
}

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
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

public sealed class CreateRoleHandler(
    IRoleRepository roleRepository,
    ITenantRepository tenantRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<CreateRoleCommand, Result<RoleResponse>>
{
    public async Task<Result<RoleResponse>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.TierEnterprise.Key))
            return Result<RoleResponse>.Forbidden("The custom roles feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(orgId, cancellationToken);
        if (tenant is null)
            return Result<RoleResponse>.NotFound($"Organization '{orgId}' not found.");

        if (tenant.Kind == TenantKind.Solo)
            return Result<RoleResponse>.BadRequest("Custom roles can only be created in team or organization tenants.");

        var permissions = new List<Permission>();
        foreach (var key in command.Permissions)
        {
            if (!Permission.TryParse(key, out var permission))
                return Result<RoleResponse>.BadRequest($"Permission '{key}' is not a valid permission string.");

            permissions.Add(permission);
        }

        var existing = await roleRepository.GetByNameAsync(orgId, command.Name, cancellationToken);
        if (existing is not null)
            return Result<RoleResponse>.BadRequest($"A role named '{command.Name}' already exists in this organization.");

        var role = Role.CreateCustom(tenant.Id, tenant.Kind, command.Name, command.Description, permissions);
        await roleRepository.AddAsync(role, cancellationToken);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.Role.ToString(),
            Action: AuditAction.Created.ToString(),
            ResourceId: role.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        events.CollectEvent(new RoleCreated(role.Id, orgId));

        return role.ToResponse(memberCount: 0);
    }
}
