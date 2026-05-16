using SharedKernel.Authentication;

namespace Main.Features.Scheduling.Shared;

public static class SchedulingAuthorization
{
    public const string ManageSchedulesForbiddenMessage = "Only owners and admins can manage schedules.";

    public const string ManageEventTypesForbiddenMessage = "Only owners and admins can manage event types.";

    public static bool CanManageSchedulingSetup(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
