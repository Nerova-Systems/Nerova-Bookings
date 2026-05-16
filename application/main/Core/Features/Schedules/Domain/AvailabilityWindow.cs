namespace Main.Features.Schedules.Domain;

public sealed record AvailabilityWindow(int[] Days, int StartMinute, int EndMinute)
{
    public AvailabilityWindow Normalize()
    {
        return this with { Days = Days.Distinct().Order().ToArray() };
    }
}
