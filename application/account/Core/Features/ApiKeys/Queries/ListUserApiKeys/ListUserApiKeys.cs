using Account.Features.ApiKeys.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.ApiKeys.Queries.ListUserApiKeys;

/// <summary>Read-only summary of a single API key (never includes the plaintext).</summary>
[PublicAPI]
public sealed record ApiKeyResponse(
    ApiKeyId Id,
    string Name,
    ApiKeyScope Scope,
    string KeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt
);

[PublicAPI]
[RequirePermission(PermissionResource.ApiKey, PermissionAction.Manage)]
public sealed record ListUserApiKeysQuery : ICommand, IRequest<Result<IReadOnlyList<ApiKeyResponse>>>;

public sealed class ListUserApiKeysHandler(
    IApiKeyRepository apiKeyRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListUserApiKeysQuery, Result<IReadOnlyList<ApiKeyResponse>>>
{
    public async Task<Result<IReadOnlyList<ApiKeyResponse>>> Handle(ListUserApiKeysQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapApiKeys.Key))
        {
            return Result<IReadOnlyList<ApiKeyResponse>>.Forbidden("The API keys feature is not enabled for this tenant.");
        }

        var keys = await apiKeyRepository.GetByUserAsync(executionContext.TenantId!, cancellationToken);
        return keys.Select(ToResponse).ToList();
    }

    private static ApiKeyResponse ToResponse(ApiKey k)
    {
        return new ApiKeyResponse(k.Id, k.Name, k.Scope, k.KeyPrefix, k.CreatedAt, k.ExpiresAt, k.RevokedAt, k.LastUsedAt);
    }
}
