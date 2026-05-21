using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Account.Features.ApiKeys.Domain;
using Account.Features.Users.Domain;
using Account.Features.Users.Shared;
using SharedKernel.Authentication;
using SharedKernel.Authentication.ApiKey;
using SharedKernel.Authentication.TokenGeneration;

namespace Account.Features.ApiKeys.Infrastructure;

/// <summary>
///     Validates a Nerova API key token by hashing it, looking it up in the database,
///     and building a <see cref="ClaimsPrincipal" /> that matches the JWT-based identity contract.
///     <para>
///         <see cref="ApiKey.MarkUsed" /> is called here and the entity is marked dirty; the
///         <c>UnitOfWorkPipelineBehavior</c> that runs at the end of every successful MediatR request
///         will persist the change without requiring a separate <c>SaveChanges</c> call here.
///     </para>
/// </summary>
public sealed class ApiKeyValidator(
    IApiKeyRepository apiKeyRepository,
    IUserRepository userRepository,
    UserInfoFactory userInfoFactory,
    TimeProvider timeProvider
) : IApiKeyValidator
{
    public async Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = ComputeHash(token);

        var apiKey = await apiKeyRepository.GetByHashAsync(hash, cancellationToken);
        if (apiKey is null || !apiKey.IsValid(timeProvider.GetUtcNow())) return null;

        // User is resolved cross-tenant so we bypass the normal tenant filter.
        var user = await userRepository.GetByIdUnfilteredAsync(apiKey.CreatedByUserId, cancellationToken);
        if (user is null) return null;

        // For org-scope keys the creator authenticates in the context of the org.
        var activeOrgId = apiKey.Scope == ApiKeyScope.Organization ? apiKey.TenantId : null;

        var userInfoResult = await userInfoFactory.CreateUserInfoAsync(
            user, sessionId: null, cancellationToken, activeOrgId: activeOrgId
        );
        if (!userInfoResult.IsSuccess) return null;

        var userInfo = userInfoResult.Value!;

        // Mark as used — change is flushed by UnitOfWorkPipelineBehavior at end of request.
        apiKey.MarkUsed(timeProvider.GetUtcNow());
        apiKeyRepository.Update(apiKey);

        return BuildPrincipal(userInfo, ApiKeyAuthenticationDefaults.SchemeName);
    }

    private static string ComputeHash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }

    private static ClaimsPrincipal BuildPrincipal(UserInfo userInfo, string schemeName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userInfo.Id!.ToString()),
            new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
            new(ClaimTypes.GivenName, userInfo.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, userInfo.LastName ?? string.Empty),
            new(ClaimTypes.Role, userInfo.Role ?? string.Empty),
            new("tenant_id", userInfo.TenantId!.ToString()),
            new("tenant_name", userInfo.TenantName ?? string.Empty),
            new("tenant_logo_url", userInfo.TenantLogoUrl ?? string.Empty),
            new("subscription_plan", userInfo.SubscriptionPlan ?? string.Empty),
            new("title", userInfo.Title ?? string.Empty),
            new("avatar_url", userInfo.AvatarUrl ?? string.Empty),
            new("locale", userInfo.Locale ?? string.Empty),
            new("session_id", string.Empty),
            new(AuthenticationTokenHttpKeys.FeatureFlagsClaimName, string.Join(",", userInfo.FeatureFlags)),
            new("tenant_rollout_bucket", userInfo.TenantRolloutBucket.ToString()),
            new("user_rollout_bucket", userInfo.UserRolloutBucket?.ToString() ?? string.Empty),
            new("active_team_id", string.Empty),
            new("active_org_id", userInfo.ActiveOrgId?.ToString() ?? string.Empty),
            new("active_org_profile_id", string.Empty)
        };

        var identity = new ClaimsIdentity(claims, schemeName, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
