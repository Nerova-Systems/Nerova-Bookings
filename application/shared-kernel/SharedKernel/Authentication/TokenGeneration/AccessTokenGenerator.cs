using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Authentication.TokenSigning;

namespace SharedKernel.Authentication.TokenGeneration;

public sealed class AccessTokenGenerator(ITokenSigningClient tokenSigningClient, TimeProvider timeProvider)
{
    // Access tokens should only be valid for a very short time and cannot be revoked.
    // For example, if a user gets a new role, the changes will not take effect until the access token expires.
    private const int ValidForMinutes = 5;

    public string Generate(UserInfo userInfo)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userInfo.Id!),
            new(JwtRegisteredClaimNames.Email, userInfo.Email!),
            new(JwtRegisteredClaimNames.GivenName, userInfo.FirstName ?? string.Empty),
            new(JwtRegisteredClaimNames.FamilyName, userInfo.LastName ?? string.Empty),
            new(ClaimTypes.Role, userInfo.Role!),
            new("tenant_id", userInfo.TenantId!.ToString()),
            new("tenant_name", userInfo.TenantName ?? string.Empty),
            new("tenant_logo_url", userInfo.TenantLogoUrl ?? string.Empty),
            new("subscription_plan", userInfo.SubscriptionPlan ?? string.Empty),
            new("title", userInfo.Title ?? string.Empty),
            new("avatar_url", userInfo.AvatarUrl ?? string.Empty),
            new("locale", userInfo.Locale!),
            new("session_id", userInfo.SessionId?.ToString() ?? string.Empty),
            new(AuthenticationTokenHttpKeys.FeatureFlagsClaimName, string.Join(",", userInfo.FeatureFlags)),
            new("tenant_rollout_bucket", userInfo.TenantRolloutBucket.ToString()),
            new("user_rollout_bucket", userInfo.UserRolloutBucket?.ToString() ?? string.Empty),
            new("active_team_id", userInfo.ActiveTeamId?.ToString() ?? string.Empty),
            new("active_org_id", userInfo.ActiveOrgId?.ToString() ?? string.Empty),
            new("active_org_profile_id", userInfo.ActiveOrgProfileId ?? string.Empty)
        };

        if (userInfo.ImpersonatedByIdentifier is not null)
        {
            claims.Add(new Claim("impersonated_by", userInfo.ImpersonatedByIdentifier));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims)
        };

        var now = timeProvider.GetUtcNow();
        return tokenDescriptor.GenerateToken(
            now,
            now.AddMinutes(ValidForMinutes),
            tokenSigningClient.Issuer,
            tokenSigningClient.Audience,
            tokenSigningClient.GetSigningCredentials()
        );
    }
}
