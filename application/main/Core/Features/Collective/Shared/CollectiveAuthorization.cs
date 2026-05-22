using SharedKernel.Authentication;

namespace Main.Features.Collective.Shared;

public static class CollectiveAuthorization
{
    public const string ManageCollectiveHostsForbiddenMessage = "Only owners and admins can manage collective hosts.";

    public const string CollectiveFeatureDisabledMessage = "The collective scheduling feature is not enabled for your account.";

    public const string CollectiveFeatureFlagKey = "cap-collective";

    public static bool HasCollectiveFeature(UserInfo userInfo)
    {
        return userInfo.IsFeatureFlagEnabled(CollectiveFeatureFlagKey);
    }

    public static bool CanManageCollectiveHosts(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
