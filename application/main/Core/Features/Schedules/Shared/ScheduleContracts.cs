using JetBrains.Annotations;
using Main.Features.Schedules.Domain;

namespace Main.Features.Schedules.Shared;

[PublicAPI]
public sealed record AvailabilityWindowRequest(int[] Days, int StartMinute, int EndMinute)
{
    public AvailabilityWindow ToAvailabilityWindow()
    {
        return new AvailabilityWindow(Days, StartMinute, EndMinute);
    }
}

[PublicAPI]
public sealed record AvailabilityWindowResponse(int[] Days, int StartMinute, int EndMinute);

[PublicAPI]
public sealed record ScheduleResponse(ScheduleId Id, string Name, string TimeZone, bool IsDefault, AvailabilityWindowResponse[] AvailabilityWindows)
{
    public static ScheduleResponse From(Schedule schedule)
    {
        return new ScheduleResponse(
            schedule.Id,
            schedule.Name,
            schedule.TimeZone,
            schedule.IsDefault,
            schedule.AvailabilityWindows
                .Select(window => new AvailabilityWindowResponse(window.Days, window.StartMinute, window.EndMinute))
                .ToArray()
        );
    }
}

[PublicAPI]
public sealed record SchedulesResponse(ScheduleResponse[] Schedules);
