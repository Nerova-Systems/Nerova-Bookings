using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Scheduling.Shared;

/// <summary>
///     Computes available slots for collective (team) event types by requiring all hosts to be free.
///     A slot is only offered when no host has a conflicting booking.
///     If the host list is empty, all candidate slots pass the host check (fallback to owner availability).
/// </summary>
public sealed class CollectiveSlotCalculator(TimeProvider timeProvider)
{
    public Dictionary<string, PublicSlotResponse[]> GetSlots(
        EventType eventType,
        Schedule schedule,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string timeZone,
        int duration
    )
    {
        var scheduleTimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone);
        var requestTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var now = timeProvider.GetUtcNow();
        var earliestStart = now.AddMinutes(eventType.MinimumBookingNoticeMinutes + (eventType.Settings.Limits.FirstAvailableSlotMinutes ?? 0));
        var slots = new List<PublicSlotResponse>();
        var firstLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startTime, scheduleTimeZone).DateTime);
        var lastLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(endTime, scheduleTimeZone).DateTime).AddDays(1);

        for (var date = firstLocalDate; date <= lastLocalDate; date = date.AddDays(1))
        {
            foreach (var window in GetWindows(schedule, date))
            {
                var localStart = date.ToDateTime(TimeOnly.MinValue).AddMinutes(window.StartMinute + (eventType.Settings.Limits.OffsetStartMinutes ?? 0));
                var localEnd = date.ToDateTime(TimeOnly.MinValue).AddMinutes(window.EndMinute);

                for (var candidate = localStart; candidate.AddMinutes(duration) <= localEnd; candidate = candidate.AddMinutes(eventType.SlotIntervalMinutes))
                {
                    if (candidate.AddMinutes(-eventType.BeforeEventBufferMinutes) < localStart ||
                        candidate.AddMinutes(duration + eventType.AfterEventBufferMinutes) > localEnd)
                    {
                        continue;
                    }

                    var candidateStart = new DateTimeOffset(candidate, scheduleTimeZone.GetUtcOffset(candidate)).ToUniversalTime();
                    var candidateEnd = candidateStart.AddMinutes(duration);
                    if (candidateStart < startTime || candidateEnd > endTime || candidateStart < earliestStart) continue;
                    if (!IsInsideBookingWindow(eventType, candidateStart, requestTimeZone, now)) continue;
                    if (AnyHostHasConflict(eventType, hostBookings, candidateStart, candidateEnd)) continue;

                    slots.Add(new PublicSlotResponse(candidateStart, candidateEnd));
                }
            }
        }

        var formatter = new DateOnlyFormatter(requestTimeZone);
        return slots
            .Distinct()
            .OrderBy(slot => slot.Time)
            .GroupBy(slot => formatter.Format(slot.Time))
            .ToDictionary(
                group => group.Key,
                group => group.Select(slot => new PublicSlotResponse(slot.Time, slot.EndTime)).ToArray(),
                StringComparer.Ordinal
            );
    }

    public bool IsSlotAvailable(EventType eventType, Schedule schedule, IReadOnlyDictionary<UserId, Booking[]> hostBookings, DateTimeOffset startTime, int duration, string timeZone)
    {
        return GetSlots(eventType, schedule, hostBookings, startTime, startTime.AddMinutes(duration), timeZone, duration)
            .Values
            .SelectMany(slots => slots)
            .Any(slot => slot.Time == startTime);
    }

    private static AvailabilityOverrideWindow[] GetWindows(Schedule schedule, DateOnly date)
    {
        var overrideForDate = schedule.DateOverrides.FirstOrDefault(dateOverride => dateOverride.Date == date);
        if (overrideForDate is not null) return overrideForDate.Windows.ToArray();

        var day = (int)date.DayOfWeek;
        return schedule.AvailabilityWindows
            .Where(window => window.Days.Contains(day))
            .Select(window => new AvailabilityOverrideWindow(window.StartMinute, window.EndMinute))
            .ToArray();
    }

    private static bool IsInsideBookingWindow(EventType eventType, DateTimeOffset candidateStart, TimeZoneInfo requestTimeZone, DateTimeOffset now)
    {
        var bookingWindow = eventType.Settings.BookingWindow;
        var candidateDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(candidateStart, requestTimeZone).DateTime);
        if (bookingWindow.FixedStartDate is not null && candidateDate < bookingWindow.FixedStartDate) return false;
        if (bookingWindow.FixedEndDate is not null && candidateDate > bookingWindow.FixedEndDate) return false;
        if (bookingWindow.RollingWindowDays is not null && candidateStart > now.AddDays(bookingWindow.RollingWindowDays.Value)) return false;
        return true;
    }

    /// <summary>
    ///     Returns true if ANY host has a booking that conflicts with the candidate slot.
    ///     When <paramref name="hostBookings" /> is empty (no hosts configured), returns false
    ///     so all candidate slots pass and availability falls back to the event owner check.
    /// </summary>
    private static bool AnyHostHasConflict(
        EventType eventType,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        DateTimeOffset candidateStart,
        DateTimeOffset candidateEnd)
    {
        var candidateConflictStart = candidateStart.AddMinutes(-eventType.BeforeEventBufferMinutes);
        var candidateConflictEnd = candidateEnd.AddMinutes(eventType.AfterEventBufferMinutes);

        foreach (var bookings in hostBookings.Values)
        {
            foreach (var booking in bookings)
            {
                var bookingConflictStart = booking.StartTime.AddMinutes(-booking.BeforeEventBufferMinutes);
                var bookingConflictEnd = booking.EndTime.AddMinutes(booking.AfterEventBufferMinutes);
                if (candidateConflictStart < bookingConflictEnd && candidateConflictEnd > bookingConflictStart)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class DateOnlyFormatter(TimeZoneInfo timeZone)
    {
        public string Format(DateTimeOffset value)
        {
            var localDate = TimeZoneInfo.ConvertTime(value, timeZone);
            return $"{localDate.Year:D4}-{localDate.Month:D2}-{localDate.Day:D2}";
        }
    }
}
