using SharedKernel.Authentication;

namespace Main.Features.RoundRobin.Shared;

public static class RoundRobinAuthorization
{
    public const string ManageRoundRobinHostsForbiddenMessage = "Only owners and admins can manage round-robin hosts.";

    public const string RoundRobinFeatureDisabledMessage = "The round-robin scheduling feature is not enabled for your account.";

    public const string RoundRobinFeatureFlagKey = "cap-round-robin";

    public static bool HasRoundRobinFeature(UserInfo userInfo)
    {
        return userInfo.IsFeatureFlagEnabled(RoundRobinFeatureFlagKey);
    }

    public static bool CanManageRoundRobinHosts(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
