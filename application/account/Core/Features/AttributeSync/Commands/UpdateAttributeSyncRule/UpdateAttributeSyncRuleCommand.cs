using Account.Features.Attributes.Domain;
using Account.Features.AttributeSync.Domain;
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

namespace Account.Features.AttributeSync.Commands.UpdateAttributeSyncRule;

[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record UpdateAttributeSyncRuleCommand : ICommand, IRequest<Result<AttributeSyncRuleResponse>>
{
    [JsonIgnore] // Removes this property from the API contract
    public required AttributeSyncRuleId RuleId { get; init; }

    public required AttributeId AttributeId { get; init; }

    public required string ClaimPath { get; init; }

    public required ClaimMappingMode Mode { get; init; }

    public bool AutoCreateOptions { get; init; }

    public bool IsEnabled { get; init; }
}

public sealed class UpdateAttributeSyncRuleValidator : AbstractValidator<UpdateAttributeSyncRuleCommand>
{
    public UpdateAttributeSyncRuleValidator()
    {
        RuleFor(x => x.ClaimPath)
            .NotEmpty()
            .MaximumLength(255);
    }
}

public sealed class UpdateAttributeSyncRuleHandler(
    IAttributeSyncRuleRepository ruleRepository,
    IAttributeRepository attributeRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<UpdateAttributeSyncRuleCommand, Result<AttributeSyncRuleResponse>>
{
    public async Task<Result<AttributeSyncRuleResponse>> Handle(
        UpdateAttributeSyncRuleCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapIntegrationAttributeSync.Key))
        {
            return Result<AttributeSyncRuleResponse>.Forbidden("The IdP attribute sync feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;

        var rule = await ruleRepository.GetByIdUnfilteredAsync(command.RuleId, cancellationToken);
        if (rule is null)
        {
            return Result<AttributeSyncRuleResponse>.NotFound($"Attribute sync rule '{command.RuleId}' not found.");
        }

        if (rule.TenantId != orgId)
        {
            return Result<AttributeSyncRuleResponse>.Forbidden("You do not have access to this rule.");
        }

        // Validate attribute belongs to this org.
        var attribute = await attributeRepository.GetByIdUnfilteredAsync(command.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result<AttributeSyncRuleResponse>.NotFound($"Attribute '{command.AttributeId}' not found.");
        }

        if (attribute.TenantId != orgId)
        {
            return Result<AttributeSyncRuleResponse>.Forbidden("You do not have access to this attribute.");
        }

        rule.Update(command.AttributeId, command.ClaimPath, command.Mode, command.AutoCreateOptions, command.IsEnabled);
        ruleRepository.Update(rule);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Attribute),
                nameof(AuditAction.Updated),
                rule.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        events.CollectEvent(new AttributeSyncRuleUpdated(rule.Id, orgId));

        return rule.ToResponse();
    }
}
