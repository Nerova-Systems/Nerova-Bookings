using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.Insights.Shared;

public static class InsightsAuthorization
{
    public const string InsightsFeatureDisabledMessage = "The insights feature is not enabled for your account.";

    public const string InsightsUnauthorizedMessage = "Authentication is required.";

    public static string InsightsFeatureFlagKey => FeatureFlagRegistry.CapInsights.Key;
}
