using Account.Features.Attributes.Domain;
using Account.Features.AuditLog.Domain;
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

namespace Account.Features.Attributes.Commands.UpdateAttribute;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Update, PermissionScope.Organization)]
public sealed record UpdateAttributeCommand : ICommand, IRequest<Result<AttributeResponse>>
{
    [JsonIgnore] // Removes this property from the API contract
    public AttributeId AttributeId { get; init; } = null!;

    public required string Name { get; init; }

    public bool IsLocked { get; init; }

    public bool IsWeightsEnabled { get; init; }

    public bool Enabled { get; init; }
}

public sealed class UpdateAttributeValidator : AbstractValidator<UpdateAttributeCommand>
{
    public UpdateAttributeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("Attribute name must be between 1 and 255 characters.");
    }
}

public sealed class UpdateAttributeHandler(
    IAttributeRepository attributeRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<UpdateAttributeCommand, Result<AttributeResponse>>
{
    public async Task<Result<AttributeResponse>> Handle(UpdateAttributeCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
        {
            return Result<AttributeResponse>.Forbidden("The attributes feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;

        var attribute = await attributeRepository.GetByIdUnfilteredAsync(command.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result<AttributeResponse>.NotFound($"Attribute '{command.AttributeId}' not found.");
        }

        if (attribute.TenantId != orgId)
        {
            return Result<AttributeResponse>.Forbidden("You do not have access to this attribute.");
        }

        attribute.Update(command.Name, command.IsLocked, command.IsWeightsEnabled, command.Enabled);
        attributeRepository.Update(attribute);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Attribute),
                nameof(AuditAction.Updated),
                attribute.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        events.CollectEvent(new AttributeUpdated(attribute.Id, orgId));

        return attribute.ToResponse();
    }
}
