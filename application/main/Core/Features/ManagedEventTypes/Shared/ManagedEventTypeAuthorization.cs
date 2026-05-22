using SharedKernel.Authentication;

namespace Main.Features.ManagedEventTypes.Shared;

public static class ManagedEventTypeAuthorization
{
    public const string ManageManagedEventTypesForbiddenMessage = "Only owners and admins can manage managed event types.";

    public const string ManagedEventTypesFeatureDisabledMessage = "The managed event types feature is not enabled for your account.";

    public const string ManagedEventTypesFeatureFlagKey = "cap-managed-event-types";

    public static bool CanManageManagedEventTypes(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }

    public static bool HasManagedEventTypesFeature(UserInfo userInfo)
    {
        return userInfo.IsFeatureFlagEnabled(ManagedEventTypesFeatureFlagKey);
    }
}
