using SharedKernel.Authentication;

namespace Main.Features.Workflows.Shared;

public static class WorkflowAuthorization
{
    public const string ManageWorkflowsForbiddenMessage = "Only owners and admins can manage workflows.";

    public const string WorkflowsFeatureDisabledMessage = "The workflows feature is not enabled for your account.";

    public const string WorkflowsFeatureFlagKey = "cap-workflows";

    public static bool CanManageWorkflows(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
