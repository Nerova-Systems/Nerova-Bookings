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
public sealed record AvailabilityOverrideWindowRequest(int StartMinute, int EndMinute)
{
    public AvailabilityOverrideWindow ToAvailabilityOverrideWindow()
    {
        return new AvailabilityOverrideWindow(StartMinute, EndMinute);
    }
}

[PublicAPI]
public sealed record AvailabilityOverrideWindowResponse(int StartMinute, int EndMinute);

[PublicAPI]
public sealed record AvailabilityDateOverrideRequest(DateOnly Date, AvailabilityOverrideWindowRequest[] Windows)
{
    public AvailabilityDateOverride ToAvailabilityDateOverride()
    {
        return new AvailabilityDateOverride(Date, Windows.Select(window => window.ToAvailabilityOverrideWindow()).ToArray());
    }
}

[PublicAPI]
public sealed record AvailabilityDateOverrideResponse(DateOnly Date, AvailabilityOverrideWindowResponse[] Windows);

[PublicAPI]
public sealed record ScheduleResponse(
    ScheduleId Id,
    string Name,
    string TimeZone,
    bool IsDefault,
    AvailabilityWindowResponse[] AvailabilityWindows,
    AvailabilityDateOverrideResponse[] DateOverrides
)
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
                .ToArray(),
            schedule.DateOverrides
                .Select(dateOverride => new AvailabilityDateOverrideResponse(
                        dateOverride.Date,
                        dateOverride.Windows.Select(window => new AvailabilityOverrideWindowResponse(window.StartMinute, window.EndMinute)).ToArray()
                    )
                )
                .ToArray()
        );
    }
}

[PublicAPI]
public sealed record SchedulesResponse(ScheduleResponse[] Schedules);
