using Account.Features.AuditLog.Domain;
using Account.Features.Attributes.Domain;
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

namespace Account.Features.Attributes.Commands.CreateAttribute;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Create, PermissionScope.Organization)]
public sealed record CreateAttributeCommand : ICommand, IRequest<Result<AttributeResponse>>
{
    public required string Name { get; init; }
    public required AttributeType Type { get; init; }
}

public sealed class CreateAttributeValidator : AbstractValidator<CreateAttributeCommand>
{
    public CreateAttributeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Attribute name must be between 1 and 255 characters.");
    }
}

public sealed class CreateAttributeHandler(
    IAttributeRepository attributeRepository,
    ITenantRepository tenantRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<CreateAttributeCommand, Result<AttributeResponse>>
{
    public async Task<Result<AttributeResponse>> Handle(CreateAttributeCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
            return Result<AttributeResponse>.Forbidden("The attributes feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var tenant = await tenantRepository.GetByIdAsync(orgId, cancellationToken);
        if (tenant is null)
            return Result<AttributeResponse>.NotFound($"Organization '{orgId}' not found.");

        if (tenant.Kind != TenantKind.Organization)
            return Result<AttributeResponse>.BadRequest("Attributes can only be created in organization tenants.");

        // Pre-check slug uniqueness to give a friendly error before hitting the DB constraint.
        var slug = Domain.Attribute.GenerateSlug(command.Name);
        if (await attributeRepository.SlugExistsUnfilteredAsync(orgId, slug, cancellationToken))
            return Result<AttributeResponse>.BadRequest($"An attribute with slug '{slug}' already exists in this organization.");

        var attribute = Domain.Attribute.Create(tenant.Id, tenant.Kind, command.Name, command.Type);
        await attributeRepository.AddAsync(attribute, cancellationToken);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.Attribute.ToString(),
            Action: AuditAction.Created.ToString(),
            ResourceId: attribute.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        events.CollectEvent(new AttributeCreated(attribute.Id, orgId));

        return attribute.ToResponse();
    }
}
