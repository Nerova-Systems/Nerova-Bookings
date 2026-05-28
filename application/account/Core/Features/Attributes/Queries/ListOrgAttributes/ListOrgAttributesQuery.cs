using Account.Features.Attributes.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Queries.ListOrgAttributes;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Read, PermissionScope.Organization)]
public sealed record ListOrgAttributesQuery : IRequest<Result<AttributeResponse[]>>;

public sealed class ListOrgAttributesHandler(
    IAttributeRepository attributeRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListOrgAttributesQuery, Result<AttributeResponse[]>>
{
    public async Task<Result<AttributeResponse[]>> Handle(ListOrgAttributesQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
        {
            return Result<AttributeResponse[]>.Forbidden("The attributes feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;
        var attributes = await attributeRepository.GetByOrgUnfilteredAsync(orgId, cancellationToken);

        return attributes.Select(a => a.ToResponse()).ToArray();
    }
}
