using SharedKernel.Authentication;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace Main.Features.Workflows.Shared;

public static class WorkflowAuthorization
{
    public const string ManageWorkflowsForbiddenMessage = "Only owners and admins can manage workflows.";

    public const string WorkflowsFeatureDisabledMessage = "The workflows feature is not enabled for your account.";

    public static string WorkflowsFeatureFlagKey => FeatureFlagRegistry.CapWorkflows.Key;

    public static bool CanManageWorkflows(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
