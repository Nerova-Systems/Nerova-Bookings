using SharedKernel.Authentication;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.RoundRobin.Shared;

public static class RoundRobinAuthorization
{
    public const string ManageRoundRobinHostsForbiddenMessage = "Only owners and admins can manage round-robin hosts.";

    public const string RoundRobinFeatureDisabledMessage = "The round-robin scheduling feature is not enabled for your account.";

    public static string RoundRobinFeatureFlagKey => FeatureFlagRegistry.CapRoundRobin.Key;

    public static bool HasRoundRobinFeature(UserInfo userInfo)
    {
        return userInfo.IsFeatureFlagEnabled(RoundRobinFeatureFlagKey);
    }

    public static bool CanManageRoundRobinHosts(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
