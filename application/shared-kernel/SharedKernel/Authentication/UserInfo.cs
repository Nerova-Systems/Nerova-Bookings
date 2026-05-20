using System.Security.Claims;
using SharedKernel.Authentication.TokenGeneration;
using SharedKernel.Domain;
using SharedKernel.Platform;
using SharedKernel.SinglePageApp;

namespace SharedKernel.Authentication;

/// <summary>
///     Provides details about the authenticated user making the current request, including user identity, role,
///     contact information, and additional profile details extracted from claims.
/// </summary>
public class UserInfo
{
    private const string DefaultLocale = "en-US";

    private static readonly IReadOnlySet<string> EmptyFeatureFlags = new HashSet<string>();

    /// <summary>
    ///     Represents the system user, typically used for background tasks or where no user is directly authenticated.
    /// </summary>
    public static readonly UserInfo System = new()
    {
        IsAuthenticated = false,
        Locale = DefaultLocale
    };

    public bool IsAuthenticated { get; init; }

    public string? Locale { get; init; }

    public UserId? Id { get; init; }

    public TenantId? TenantId { get; init; }

    public string? Role { get; init; }

    public string? Email { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Title { get; init; }

    public string? AvatarUrl { get; init; }

    public string? TenantName { get; init; }

    public string? TenantLogoUrl { get; init; }

    public string? SubscriptionPlan { get; init; }

    public string? ZoomLevel { get; init; }

    public string? Theme { get; init; }

    public SessionId? SessionId { get; init; }

    public bool IsInternalUser { get; init; }

    public IReadOnlySet<string> FeatureFlags { get; init; } = EmptyFeatureFlags;

    public int TenantRolloutBucket { get; init; }

    public int? UserRolloutBucket { get; init; }

    /// <summary>
    ///     The currently-active team scope, or <see langword="null" /> when not in a team session.
    ///     Populated from the <c>active_team_id</c> JWT claim after a scope switch.
    /// </summary>
    public TenantId? ActiveTeamId { get; init; }

    /// <summary>
    ///     The currently-active organization, or <see langword="null" /> when not in an org session.
    ///     For a team session this is the team's parent org; for an org session this equals <c>TenantId</c>.
    ///     Populated from the <c>active_org_id</c> JWT claim after a scope switch.
    /// </summary>
    public TenantId? ActiveOrgId { get; init; }

    /// <summary>
    ///     The OrgProfile ID the user acts as in the current org session, or <see langword="null" />.
    ///     Populated from the <c>active_org_profile_id</c> JWT claim after a scope switch.
    /// </summary>
    public string? ActiveOrgProfileId { get; init; }

    public bool IsFeatureFlagEnabled(string flagKey)
    {
        return FeatureFlags.Contains(flagKey);
    }

    public static UserInfo Create(ClaimsPrincipal? user, string? browserLocale, string? zoomLevel = null, string? theme = null)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new UserInfo
            {
                IsAuthenticated = user?.Identity?.IsAuthenticated ?? false,
                Locale = GetValidLocale(browserLocale),
                ZoomLevel = zoomLevel,
                Theme = theme
            };
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = user.FindFirstValue("tenant_id");
        var sessionId = user.FindFirstValue("session_id");
        var email = user.FindFirstValue(ClaimTypes.Email);
        var featureFlagsClaim = user.FindFirstValue(AuthenticationTokenHttpKeys.FeatureFlagsClaimName);
        var tenantRolloutBucketClaim = user.FindFirstValue("tenant_rollout_bucket");
        var userRolloutBucketClaim = user.FindFirstValue("user_rollout_bucket");
        var activeTeamIdClaim = user.FindFirstValue("active_team_id");
        var activeOrgIdClaim = user.FindFirstValue("active_org_id");
        var activeOrgProfileIdClaim = user.FindFirstValue("active_org_profile_id");
        return new UserInfo
        {
            IsAuthenticated = true,
            Id = userId == null ? null : new UserId(userId),
            TenantId = tenantId == null ? null : new TenantId(long.Parse(tenantId)),
            SessionId = sessionId == null ? null : new SessionId(sessionId),
            Role = user.FindFirstValue(ClaimTypes.Role),
            Email = user.FindFirstValue(ClaimTypes.Email),
            FirstName = user.FindFirstValue(ClaimTypes.GivenName),
            LastName = user.FindFirstValue(ClaimTypes.Surname),
            Title = user.FindFirstValue("title"),
            AvatarUrl = user.FindFirstValue("avatar_url"),
            TenantName = user.FindFirstValue("tenant_name"),
            TenantLogoUrl = user.FindFirstValue("tenant_logo_url"),
            SubscriptionPlan = user.FindFirstValue("subscription_plan"),
            Locale = GetValidLocale(user.FindFirstValue("locale")),
            ZoomLevel = zoomLevel,
            Theme = theme,
            IsInternalUser = IsInternalUserEmail(email),
            FeatureFlags = ParseFeatureFlags(featureFlagsClaim),
            TenantRolloutBucket = !string.IsNullOrEmpty(tenantRolloutBucketClaim) ? int.Parse(tenantRolloutBucketClaim) : 0,
            UserRolloutBucket = !string.IsNullOrEmpty(userRolloutBucketClaim) ? int.Parse(userRolloutBucketClaim) : null,
            ActiveTeamId = string.IsNullOrEmpty(activeTeamIdClaim) ? null : new TenantId(long.Parse(activeTeamIdClaim)),
            ActiveOrgId = string.IsNullOrEmpty(activeOrgIdClaim) ? null : new TenantId(long.Parse(activeOrgIdClaim)),
            ActiveOrgProfileId = string.IsNullOrEmpty(activeOrgProfileIdClaim) ? null : activeOrgProfileIdClaim
        };
    }

    private static IReadOnlySet<string> ParseFeatureFlags(string? claim)
    {
        if (string.IsNullOrEmpty(claim)) return EmptyFeatureFlags;
        return new HashSet<string>(claim.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsInternalUserEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return false;
        return email.EndsWith(Settings.Current.Identity.InternalEmailDomain, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetValidLocale(string? locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return DefaultLocale;
        }

        if (SinglePageAppConfiguration.SupportedLocalizations.Contains(locale, StringComparer.OrdinalIgnoreCase))
        {
            return locale;
        }

        // Fallback to base language. E.g. if locale is `en-UK` use `en` which would then return `en-US`
        var baseLanguageCode = locale[..2];
        var foundLocale = SinglePageAppConfiguration.SupportedLocalizations
            .FirstOrDefault(sl => sl.StartsWith(baseLanguageCode, StringComparison.OrdinalIgnoreCase));

        return foundLocale ?? DefaultLocale;
    }
}
