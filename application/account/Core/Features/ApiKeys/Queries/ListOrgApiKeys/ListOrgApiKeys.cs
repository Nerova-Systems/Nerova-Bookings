using Account.Features.ApiKeys.Domain;
using Account.Features.ApiKeys.Queries.ListUserApiKeys;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.ApiKeys.Queries.ListOrgApiKeys;

[PublicAPI]
[RequirePermission(PermissionResource.ApiKey, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ListOrgApiKeysQuery : ICommand, IRequest<Result<IReadOnlyList<ApiKeyResponse>>>;

public sealed class ListOrgApiKeysHandler(
    IApiKeyRepository apiKeyRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListOrgApiKeysQuery, Result<IReadOnlyList<ApiKeyResponse>>>
{
    public async Task<Result<IReadOnlyList<ApiKeyResponse>>> Handle(ListOrgApiKeysQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapApiKeys.Key))
        {
            return Result<IReadOnlyList<ApiKeyResponse>>.Forbidden("The API keys feature is not enabled for this tenant.");
        }

        var keys = await apiKeyRepository.GetByOrgAsync(executionContext.ActiveOrgId!, cancellationToken);
        return keys.Select(ToResponse).ToList();
    }

    private static ApiKeyResponse ToResponse(ApiKey k)
    {
        return new ApiKeyResponse(k.Id, k.Name, k.Scope, k.KeyPrefix, k.CreatedAt, k.ExpiresAt, k.RevokedAt, k.LastUsedAt);
    }
}
