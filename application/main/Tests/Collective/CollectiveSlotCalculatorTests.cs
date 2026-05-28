using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Collective;

/// <summary>
///     Unit tests for <see cref="CollectiveSlotCalculator" />.
///     The calculator is pure (no DB access) so tests construct domain objects directly.
/// </summary>
public sealed class CollectiveSlotCalculatorTests
{
    // Fixed reference time: Thursday 2026-01-08 12:00 UTC = 14:00 SAST
    private static readonly DateTimeOffset ReferenceUtc = new(2026, 1, 8, 12, 0, 0, TimeSpan.Zero);

    // Schedule: Mon–Fri 09:00–17:00 Africa/Johannesburg (UTC+2)
    private static readonly Schedule WorkWeekSchedule = Schedule.Create(
        new TenantId(1),
        UserId.NewId(),
        "Work Hours",
        "Africa/Johannesburg",
        true,
        [new AvailabilityWindow([1, 2, 3, 4, 5], 540, 1020)], // 9:00=540, 17:00=1020
        []
    );

    private readonly CollectiveSlotCalculator _calculator;
    private readonly EventType _eventType;

    public CollectiveSlotCalculatorTests()
    {
        // TimeProvider fixed at ReferenceUtc so EarliestStart doesn't filter out everything
        var timeProvider = new FixedTimeProvider(ReferenceUtc.AddDays(-7)); // start is 7 days ago relative to schedule window
        _calculator = new CollectiveSlotCalculator(timeProvider);

        _eventType = EventType.Create(
            new TenantId(1),
            UserId.NewId(),
            "Team Meeting",
            "team-meeting",
            null,
            30,
            false,
            WorkWeekSchedule.Id,
            0,
            0,
            30,
            0,
            null,
            null,
            null,
            new TenantId(99)
        );
        _eventType.SetSchedulingType(SchedulingType.Collective);
    }

    // ─── Empty hosts → all schedule slots offered ────────────────────────────

    [Fact]
    public void GetSlots_WhenNoHosts_ShouldReturnAllScheduleSlots()
    {
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero); // Monday 09:00 SAST
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero); // Monday 17:00 SAST

        var slots = _calculator.GetSlots(
            _eventType,
            WorkWeekSchedule,
            new Dictionary<UserId, Booking[]>(),
            start, end,
            "Africa/Johannesburg",
            30
        );

        slots.Should().NotBeEmpty();
        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        // 09:00–17:00 with 30-min slots and 30-min duration = 16 slots
        allSlots.Should().HaveCount(16);
    }

    // ─── Single busy host blocks a slot ──────────────────────────────────────

    [Fact]
    public void GetSlots_WhenOneHostBusyAt0900_ShouldExcludeNineAmSlot()
    {
        var hostUserId = UserId.NewId();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // Host booked 09:00–09:30 SAST = 07:00–07:30 UTC
        var busyStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var hostBooking = CreateBooking(hostUserId, busyStart, busyStart.AddMinutes(30));

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [hostUserId] = [hostBooking]
        };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        // 09:00 slot should be excluded because host is busy
        allSlots.Should().NotContain(s => s.Time == busyStart);
        // All other slots (15 remaining) should be offered
        allSlots.Should().HaveCount(15);
    }

    // ─── Multiple hosts — all must be free ───────────────────────────────────

    [Fact]
    public void GetSlots_WhenTwoHostsEachBusyAtDifferentSlots_ShouldExcludeBothBusySlots()
    {
        var host1 = UserId.NewId();
        var host2 = UserId.NewId();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // Host1 busy at 09:00–09:30 UTC (=09:00 SAST)
        var busyStart1 = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        // Host2 busy at 09:30–10:00 UTC (=11:30 SAST)
        var busyStart2 = new DateTimeOffset(2026, 1, 12, 9, 30, 0, TimeSpan.Zero);

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1] = [CreateBooking(host1, busyStart1, busyStart1.AddMinutes(30))],
            [host2] = [CreateBooking(host2, busyStart2, busyStart2.AddMinutes(30))]
        };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().NotContain(s => s.Time == busyStart1);
        allSlots.Should().NotContain(s => s.Time == busyStart2);
        allSlots.Should().HaveCount(14); // 16 slots - 2 busy
    }

    // ─── Host busy in different window — overlapping slot excluded ────────────

    [Fact]
    public void GetSlots_WhenOneHostBusySpanningMultipleSlots_ShouldExcludeAllConflictingSlots()
    {
        var hostUserId = UserId.NewId();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // Host busy 09:00–10:30 SAST = 07:00–08:30 UTC
        // Blocks:
        //   09:00 SAST (07:00–07:30 UTC) — overlaps booking 07:00–08:30
        //   09:30 SAST (07:30–08:00 UTC) — overlaps booking 07:00–08:30
        //   10:00 SAST (08:00–08:30 UTC) — overlaps booking 07:00–08:30 (08:00 < 08:30 && 08:30 > 07:00)
        // First free slot: 10:30 SAST (08:30–09:00 UTC), since 08:30 < 08:30 is false
        var busyStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var busyEnd = new DateTimeOffset(2026, 1, 12, 8, 30, 0, TimeSpan.Zero);
        var hostBooking = CreateBooking(hostUserId, busyStart, busyEnd);

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [hostUserId] = [hostBooking]
        };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        // 09:00, 09:30, 10:00 blocked; 10:30 is first available
        allSlots.Should().NotContain(s => s.Time == new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero));
        allSlots.Should().NotContain(s => s.Time == new DateTimeOffset(2026, 1, 12, 7, 30, 0, TimeSpan.Zero));
        allSlots.Should().NotContain(s => s.Time == new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero));
        allSlots.Should().HaveCount(13); // 16 total - 3 blocked
    }

    // ─── IsSlotAvailable ─────────────────────────────────────────────────────

    [Fact]
    public void IsSlotAvailable_WhenAllHostsFree_ShouldReturnTrue()
    {
        var hostUserId = UserId.NewId();
        // Host has booking in a different time window
        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [hostUserId] = [CreateBooking(hostUserId, new DateTimeOffset(2026, 1, 12, 11, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 12, 11, 30, 0, TimeSpan.Zero))]
        };

        // Check 09:00 SAST slot (07:00 UTC)
        var slotStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var available = _calculator.IsSlotAvailable(
            _eventType, WorkWeekSchedule, hostBookings, slotStart, 30, "Africa/Johannesburg"
        );

        available.Should().BeTrue();
    }

    [Fact]
    public void IsSlotAvailable_WhenOneHostBusy_ShouldReturnFalse()
    {
        var hostUserId = UserId.NewId();
        var slotStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero); // 09:00 SAST
        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [hostUserId] = [CreateBooking(hostUserId, slotStart, slotStart.AddMinutes(30))]
        };

        var available = _calculator.IsSlotAvailable(
            _eventType, WorkWeekSchedule, hostBookings, slotStart, 30, "Africa/Johannesburg"
        );

        available.Should().BeFalse();
    }

    // ─── Buffer interaction ───────────────────────────────────────────────────

    [Fact]
    public void GetSlots_WhenBookingHasAfterBuffer_ShouldExcludeOverlappingNextSlot()
    {
        var hostUserId = UserId.NewId();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // Booking 09:00–09:30 SAST + 30-min after buffer → effective end 10:00 SAST (08:00 UTC)
        var bookingStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var booking = Booking.Create(
            new TenantId(1), hostUserId, _eventType.Id,
            bookingStart, 30,
            0,
            30,
            "Booker", "booker@example.com", "UTC", BookingStatus.Accepted,
            new Dictionary<string, string>()
        );

        var hostBookings = new Dictionary<UserId, Booking[]> { [hostUserId] = [booking] };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        // Both 09:00 and 09:30 SAST slots excluded (booking + after buffer covers both)
        allSlots.Should().NotContain(s => s.Time == bookingStart);
        allSlots.Should().NotContain(s => s.Time == bookingStart.AddMinutes(30));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private Booking CreateBooking(UserId owner, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var durationMinutes = (int)(endTime - startTime).TotalMinutes;
        return Booking.Create(
            new TenantId(1),
            owner,
            _eventType.Id,
            startTime,
            durationMinutes,
            0,
            0,
            "Test Booker",
            "booker@example.com",
            "UTC", BookingStatus.Accepted,
            new Dictionary<string, string>()
        );
    }

    private sealed class FixedTimeProvider(DateTimeOffset fixedTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return fixedTime;
        }
    }
}
