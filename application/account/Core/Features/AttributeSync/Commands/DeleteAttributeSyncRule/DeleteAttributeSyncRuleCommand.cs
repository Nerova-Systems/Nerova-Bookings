using Account.Features.AuditLog.Domain;
using Account.Features.AttributeSync.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.AttributeSync.Commands.DeleteAttributeSyncRule;

[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record DeleteAttributeSyncRuleCommand : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes this property from the API contract
    public AttributeSyncRuleId RuleId { get; init; } = default!;
}

public sealed class DeleteAttributeSyncRuleHandler(
    IAttributeSyncRuleRepository ruleRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<DeleteAttributeSyncRuleCommand, Result>
{
    public async Task<Result> Handle(DeleteAttributeSyncRuleCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapIntegrationAttributeSync.Key))
            return Result.Forbidden("The IdP attribute sync feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var rule = await ruleRepository.GetByIdUnfilteredAsync(command.RuleId, cancellationToken);
        if (rule is null)
            return Result.NotFound($"Attribute sync rule '{command.RuleId}' not found.");

        if (rule.TenantId != orgId)
            return Result.Forbidden("You do not have access to this rule.");

        ruleRepository.Remove(rule);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.Attribute.ToString(),
            Action: AuditAction.Deleted.ToString(),
            ResourceId: rule.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        events.CollectEvent(new AttributeSyncRuleDeleted(rule.Id, orgId));

        return Result.Success();
    }
}
