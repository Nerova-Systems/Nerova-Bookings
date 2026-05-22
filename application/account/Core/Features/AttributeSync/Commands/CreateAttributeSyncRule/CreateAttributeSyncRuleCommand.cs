using Account.Features.AuditLog.Domain;
using Account.Features.AttributeSync.Domain;
using Account.Features.Attributes.Domain;
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

namespace Account.Features.AttributeSync.Commands.CreateAttributeSyncRule;

[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record CreateAttributeSyncRuleCommand : ICommand, IRequest<Result<AttributeSyncRuleResponse>>
{
    public required AttributeId AttributeId { get; init; }
    public required string ClaimPath { get; init; }
    public required ClaimMappingMode Mode { get; init; }
    public bool AutoCreateOptions { get; init; }
}

public sealed class CreateAttributeSyncRuleValidator : AbstractValidator<CreateAttributeSyncRuleCommand>
{
    public CreateAttributeSyncRuleValidator()
    {
        RuleFor(x => x.ClaimPath)
            .NotEmpty()
            .MaximumLength(255);
    }
}

public sealed class CreateAttributeSyncRuleHandler(
    IAttributeSyncRuleRepository ruleRepository,
    IAttributeRepository attributeRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<CreateAttributeSyncRuleCommand, Result<AttributeSyncRuleResponse>>
{
    public async Task<Result<AttributeSyncRuleResponse>> Handle(
        CreateAttributeSyncRuleCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapIntegrationAttributeSync.Key))
            return Result<AttributeSyncRuleResponse>.Forbidden("The IdP attribute sync feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        // Validate attribute belongs to this org.
        var attribute = await attributeRepository.GetByIdUnfilteredAsync(command.AttributeId, cancellationToken);
        if (attribute is null)
            return Result<AttributeSyncRuleResponse>.NotFound($"Attribute '{command.AttributeId}' not found.");

        if (attribute.TenantId != orgId)
            return Result<AttributeSyncRuleResponse>.Forbidden("You do not have access to this attribute.");

        var rule = AttributeSyncRule.Create(orgId, command.AttributeId, command.ClaimPath, command.Mode, command.AutoCreateOptions);
        await ruleRepository.AddAsync(rule, cancellationToken);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.Attribute.ToString(),
            Action: AuditAction.Created.ToString(),
            ResourceId: rule.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        events.CollectEvent(new AttributeSyncRuleCreated(rule.Id, orgId));

        return rule.ToResponse();
    }
}
