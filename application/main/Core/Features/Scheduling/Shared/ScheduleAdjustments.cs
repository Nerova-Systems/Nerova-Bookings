using Main.Features.Schedules.Domain;

namespace Main.Features.Scheduling.Shared;

/// <summary>
///     Per-user adjustments that influence slot calculation: temporary timezone changes while
///     travelling and entire-day exclusions for out-of-office periods. Mirrors cal.com's
///     <c>getAdjustedTimezone</c> and <c>datesOutOfOffice</c> behaviour.
/// </summary>
/// <param name="TravelSchedules">
///     Travel schedule entries for the owning user. The first entry whose
///     <see cref="TravelSchedule.Covers" /> returns <see langword="true" /> for a given date
///     supplies the effective timezone for that date.
/// </param>
/// <param name="OutOfOffices">
///     Out-of-office entries for the owning user. Any date covered by any entry is skipped
///     entirely during slot calculation.
/// </param>
public sealed record ScheduleAdjustments(
    IReadOnlyList<TravelSchedule> TravelSchedules,
    IReadOnlyList<OutOfOffice> OutOfOffices
)
{
    public static readonly ScheduleAdjustments Empty = new([], []);

    /// <summary>
    ///     Returns the timezone to use when interpreting local clock times on <paramref name="date" />.
    ///     If a travel schedule covers the date, its timezone wins; otherwise
    ///     <paramref name="defaultTimeZone" /> is returned.
    /// </summary>
    public TimeZoneInfo GetEffectiveTimeZone(DateOnly date, TimeZoneInfo defaultTimeZone)
    {
        foreach (var travel in TravelSchedules)
        {
            if (!travel.Covers(date)) continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(travel.TimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                // Malformed travel timezone — fall through to default rather than break slot calc.
            }
            catch (InvalidTimeZoneException)
            {
                // Malformed travel timezone — fall through to default rather than break slot calc.
            }
        }

        return defaultTimeZone;
    }

    public bool IsOutOfOffice(DateOnly date)
    {
        foreach (var outOfOffice in OutOfOffices)
        {
            if (outOfOffice.Covers(date)) return true;
        }

        return false;
    }
}
