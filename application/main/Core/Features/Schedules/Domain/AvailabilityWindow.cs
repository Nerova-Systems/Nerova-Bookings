namespace Main.Features.Schedules.Domain;

public sealed record AvailabilityWindow(int[] Days, int StartMinute, int EndMinute)
{
    public AvailabilityWindow Normalize()
    {
        return this with { Days = Days.Distinct().Order().ToArray() };
    }
}

public sealed record AvailabilityDateOverride(DateOnly Date, AvailabilityOverrideWindow[] Windows)
{
    public AvailabilityDateOverride Normalize()
    {
        return this with { Windows = [.. Windows.OrderBy(window => window.StartMinute).ThenBy(window => window.EndMinute)] };
    }
}

public sealed record AvailabilityOverrideWindow(int StartMinute, int EndMinute);
