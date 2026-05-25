using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Scheduling.Shared;

/// <summary>
///     Computes available slots for round-robin event types.
///     A slot is available when ALL fixed hosts are free AND AT LEAST ONE rotating host is free.
///     If there are no rotating hosts, only fixed-host constraints apply.
///     If there are no fixed hosts, only rotating constraints apply.
///     If there are no hosts at all, all candidate slots pass (fallback to owner availability).
/// </summary>
public sealed class RoundRobinSlotCalculator(TimeProvider timeProvider)
{
    public Dictionary<string, PublicSlotResponse[]> GetSlots(
        EventType eventType,
        Schedule schedule,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        IReadOnlyList<Host> hosts,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string timeZone,
        int duration,
        ScheduleAdjustments? adjustments = null
    )
    {
        var scheduleTimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone);
        var requestTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var effectiveAdjustments = adjustments ?? ScheduleAdjustments.Empty;
        var now = timeProvider.GetUtcNow();
        var earliestStart = now.AddMinutes(eventType.MinimumBookingNoticeMinutes + (eventType.Settings.Limits.FirstAvailableSlotMinutes ?? 0));
        var slots = new List<PublicSlotResponse>();
        var firstLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startTime, scheduleTimeZone).DateTime);
        var lastLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(endTime, scheduleTimeZone).DateTime).AddDays(1);

        var fixedHosts = hosts.Where(h => h.IsFixed).Select(h => h.UserId).ToArray();
        var rotatingHosts = hosts.Where(h => !h.IsFixed).Select(h => h.UserId).ToArray();

        for (var date = firstLocalDate; date <= lastLocalDate; date = date.AddDays(1))
        {
            if (effectiveAdjustments.IsOutOfOffice(date)) continue;
            var dateTimeZone = effectiveAdjustments.GetEffectiveTimeZone(date, scheduleTimeZone);
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

                    var candidateStart = new DateTimeOffset(candidate, dateTimeZone.GetUtcOffset(candidate)).ToUniversalTime();
                    var candidateEnd = candidateStart.AddMinutes(duration);
                    if (candidateStart < startTime || candidateEnd > endTime || candidateStart < earliestStart) continue;
                    if (!IsInsideBookingWindow(eventType, candidateStart, requestTimeZone, now)) continue;
                    if (!IsSlotValid(eventType, hostBookings, fixedHosts, rotatingHosts, candidateStart, candidateEnd)) continue;

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

    public bool IsSlotAvailable(
        EventType eventType,
        Schedule schedule,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        IReadOnlyList<Host> hosts,
        DateTimeOffset startTime,
        int duration,
        string timeZone,
        ScheduleAdjustments? adjustments = null)
    {
        return GetSlots(eventType, schedule, hostBookings, hosts, startTime, startTime.AddMinutes(duration), timeZone, duration, adjustments)
            .Values
            .SelectMany(slots => slots)
            .Any(slot => slot.Time == startTime);
    }

    /// <summary>
    ///     Selects the best available rotating host for the given candidate start time using
    ///     weighted least-busy selection: among conflict-free rotating hosts in the highest-priority
    ///     tier, returns the one with the lowest booking-count-to-weight ratio (tie-broken by
    ///     earliest last-booking timestamp).
    /// </summary>
    /// <returns>The <see cref="UserId" /> of the selected host, or <see langword="null" /> if no rotating host is available.</returns>
    public UserId? SelectRoundRobinHost(
        IReadOnlyList<Host> hosts,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        DateTimeOffset candidateStart,
        int durationMinutes,
        int beforeBufferMinutes,
        int afterBufferMinutes)
    {
        var rotatingHosts = hosts.Where(h => !h.IsFixed).ToArray();
        if (rotatingHosts.Length == 0) return null;

        var candidateEnd = candidateStart.AddMinutes(durationMinutes);
        var availableRotating = rotatingHosts
            .Where(h => !HasConflict(h.UserId, hostBookings, candidateStart, candidateEnd, beforeBufferMinutes, afterBufferMinutes))
            .ToArray();

        if (availableRotating.Length == 0) return null;

        // Group by priority ascending (lower value = higher real priority)
        var topPriority = availableRotating.Min(h => h.Priority);
        var topTier = availableRotating.Where(h => h.Priority == topPriority).ToArray();

        // Weighted least-busy: score = bookingCount / weight (lower = preferred)
        var selected = topTier
            .OrderBy(h =>
            {
                var bookingCount = hostBookings.TryGetValue(h.UserId, out var bookings) ? bookings.Length : 0;
                return (double)bookingCount / h.Weight;
            })
            .ThenBy(h =>
            {
                if (!hostBookings.TryGetValue(h.UserId, out var bookings) || bookings.Length == 0) return DateTimeOffset.MinValue;
                return bookings.Max(b => b.StartTime);
            })
            .First();

        return selected.UserId;
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
    ///     Returns true when the candidate slot satisfies the round-robin availability rules:
    ///     ALL fixed hosts are free AND AT LEAST ONE rotating host is free.
    ///     Degenerate cases: no hosts → always available; no rotating hosts → only fixed-host rule applies;
    ///     no fixed hosts → only rotating rule applies.
    /// </summary>
    private static bool IsSlotValid(
        EventType eventType,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        UserId[] fixedHosts,
        UserId[] rotatingHosts,
        DateTimeOffset candidateStart,
        DateTimeOffset candidateEnd)
    {
        var candidateConflictStart = candidateStart.AddMinutes(-eventType.BeforeEventBufferMinutes);
        var candidateConflictEnd = candidateEnd.AddMinutes(eventType.AfterEventBufferMinutes);

        // All fixed hosts must be free
        foreach (var fixedUserId in fixedHosts)
        {
            if (UserHasConflict(fixedUserId, hostBookings, candidateConflictStart, candidateConflictEnd))
            {
                return false;
            }
        }

        // At least one rotating host must be free (if any exist)
        if (rotatingHosts.Length > 0)
        {
            var anyRotatingFree = rotatingHosts.Any(userId => !UserHasConflict(userId, hostBookings, candidateConflictStart, candidateConflictEnd));
            if (!anyRotatingFree) return false;
        }

        return true;
    }

    private static bool UserHasConflict(
        UserId userId,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        DateTimeOffset conflictStart,
        DateTimeOffset conflictEnd)
    {
        if (!hostBookings.TryGetValue(userId, out var bookings)) return false;

        foreach (var booking in bookings)
        {
            var bookingConflictStart = booking.StartTime.AddMinutes(-booking.BeforeEventBufferMinutes);
            var bookingConflictEnd = booking.EndTime.AddMinutes(booking.AfterEventBufferMinutes);
            if (conflictStart < bookingConflictEnd && conflictEnd > bookingConflictStart)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConflict(
        UserId userId,
        IReadOnlyDictionary<UserId, Booking[]> hostBookings,
        DateTimeOffset candidateStart,
        DateTimeOffset candidateEnd,
        int beforeBuffer,
        int afterBuffer)
    {
        var conflictStart = candidateStart.AddMinutes(-beforeBuffer);
        var conflictEnd = candidateEnd.AddMinutes(afterBuffer);
        return UserHasConflict(userId, hostBookings, conflictStart, conflictEnd);
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
