using Account.Features.AttributeSync.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.AttributeSync.Queries.ListAttributeSyncRules;

[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ListAttributeSyncRulesQuery : IRequest<Result<AttributeSyncRuleResponse[]>>;

public sealed class ListAttributeSyncRulesHandler(
    IAttributeSyncRuleRepository ruleRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListAttributeSyncRulesQuery, Result<AttributeSyncRuleResponse[]>>
{
    public async Task<Result<AttributeSyncRuleResponse[]>> Handle(
        ListAttributeSyncRulesQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapIntegrationAttributeSync.Key))
        {
            return Result<AttributeSyncRuleResponse[]>.Forbidden("The IdP attribute sync feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var rules = await ruleRepository.GetByOrgUnfilteredAsync(orgId, cancellationToken);

        return rules.Select(r => r.ToResponse()).ToArray();
    }
}
