using SharedKernel.Authentication;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.ManagedEventTypes.Shared;

public static class ManagedEventTypeAuthorization
{
    public const string ManageManagedEventTypesForbiddenMessage = "Only owners and admins can manage managed event types.";

    public const string ManagedEventTypesFeatureDisabledMessage = "The managed event types feature is not enabled for your account.";

    public static string ManagedEventTypesFeatureFlagKey => FeatureFlagRegistry.CapManagedEventTypes.Key;

    public static bool CanManageManagedEventTypes(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }

    public static bool HasManagedEventTypesFeature(UserInfo userInfo)
    {
        return userInfo.IsFeatureFlagEnabled(ManagedEventTypesFeatureFlagKey);
    }
}
