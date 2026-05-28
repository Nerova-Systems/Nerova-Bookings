using Main.Features.Schedules.Domain;
using SharedKernel.Domain;

namespace Main.Features.Schedules.Shared;

/// <summary>
///     Centralizes the access check for schedules. A caller may access a schedule if either
///     they own it, or the schedule is team-scoped and the caller's active team matches.
/// </summary>
public static class ScheduleAccess
{
    public static bool HasAccess(Schedule schedule, UserId callerUserId, TenantId? activeTeamId)
    {
        if (schedule.TeamId is not null)
        {
            return activeTeamId is not null && schedule.TeamId == activeTeamId;
        }

        return schedule.OwnerUserId == callerUserId;
    }
}
