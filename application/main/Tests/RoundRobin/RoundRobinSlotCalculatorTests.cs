using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.RoundRobin;

/// <summary>
///     Unit tests for <see cref="RoundRobinSlotCalculator" />.
///     The calculator is pure (no DB access) so tests construct domain objects directly.
/// </summary>
public sealed class RoundRobinSlotCalculatorTests
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

    private readonly RoundRobinSlotCalculator _calculator;
    private readonly EventType _eventType;

    public RoundRobinSlotCalculatorTests()
    {
        var timeProvider = new FixedTimeProvider(ReferenceUtc.AddDays(-7));
        _calculator = new RoundRobinSlotCalculator(timeProvider);

        _eventType = EventType.Create(
            new TenantId(1),
            UserId.NewId(),
            "Round Robin Meeting",
            "rr-meeting",
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
        _eventType.SetSchedulingType(SchedulingType.RoundRobin);
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
            [],
            start, end,
            "Africa/Johannesburg",
            30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().HaveCount(16); // 09:00–17:00, 30-min slots, 30-min duration
    }

    // ─── Only rotating hosts — at least one must be free ─────────────────────

    [Fact]
    public void GetSlots_WhenOneRotatingHostBusy_OtherFree_ShouldIncludeSlot()
    {
        var host1 = CreateRotatingHost();
        var host2 = CreateRotatingHost();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // host1 busy at 09:00 SAST
        var busyStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = [CreateBooking(host1.UserId, busyStart, busyStart.AddMinutes(30))]
        };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, [host1, host2], start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        // host2 is free so 09:00 slot should still appear
        allSlots.Should().Contain(s => s.Time == busyStart);
        allSlots.Should().HaveCount(16);
    }

    [Fact]
    public void GetSlots_WhenAllRotatingHostsBusy_ShouldExcludeSlot()
    {
        var host1 = CreateRotatingHost();
        var host2 = CreateRotatingHost();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // Both hosts busy at 09:00 SAST
        var busyStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = [CreateBooking(host1.UserId, busyStart, busyStart.AddMinutes(30))],
            [host2.UserId] = [CreateBooking(host2.UserId, busyStart, busyStart.AddMinutes(30))]
        };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, [host1, host2], start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().NotContain(s => s.Time == busyStart);
        allSlots.Should().HaveCount(15); // 16 - 1 blocked slot
    }

    // ─── Fixed host blocks a slot regardless of rotating hosts ───────────────

    [Fact]
    public void GetSlots_WhenFixedHostBusy_ShouldExcludeSlotEvenIfRotatingFree()
    {
        var fixedHost = CreateFixedHost();
        var rotatingHost = CreateRotatingHost();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        // Fixed host busy at 09:00 SAST; rotating host is free
        var busyStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [fixedHost.UserId] = [CreateBooking(fixedHost.UserId, busyStart, busyStart.AddMinutes(30))]
        };

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, hostBookings, [fixedHost, rotatingHost], start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        // Fixed host is busy so slot should be excluded
        allSlots.Should().NotContain(s => s.Time == busyStart);
        allSlots.Should().HaveCount(15);
    }

    [Fact]
    public void GetSlots_WhenOnlyFixedHostsAllFree_ShouldIncludeSlots()
    {
        var fixedHost1 = CreateFixedHost();
        var fixedHost2 = CreateFixedHost();
        var start = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 12, 15, 0, 0, TimeSpan.Zero);

        var slots = _calculator.GetSlots(
            _eventType, WorkWeekSchedule, new Dictionary<UserId, Booking[]>(), [fixedHost1, fixedHost2], start, end, "Africa/Johannesburg", 30
        );

        var allSlots = slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().HaveCount(16);
    }

    // ─── SelectRoundRobinHost ─────────────────────────────────────────────────

    [Fact]
    public void SelectRoundRobinHost_WhenOneRotatingHostAvailable_ShouldReturnThatHost()
    {
        var host = CreateRotatingHost();
        var candidateStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);

        var selected = _calculator.SelectRoundRobinHost(
            [host],
            new Dictionary<UserId, Booking[]>(),
            candidateStart, 30, 0, 0
        );

        selected.Should().Be(host.UserId);
    }

    [Fact]
    public void SelectRoundRobinHost_WhenNoRotatingHosts_ShouldReturnNull()
    {
        var fixedHost = CreateFixedHost();
        var candidateStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);

        var selected = _calculator.SelectRoundRobinHost(
            [fixedHost],
            new Dictionary<UserId, Booking[]>(),
            candidateStart, 30, 0, 0
        );

        selected.Should().BeNull();
    }

    [Fact]
    public void SelectRoundRobinHost_WhenAllRotatingHostsBusy_ShouldReturnNull()
    {
        var host = CreateRotatingHost();
        var candidateStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);
        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host.UserId] = [CreateBooking(host.UserId, candidateStart, candidateStart.AddMinutes(30))]
        };

        var selected = _calculator.SelectRoundRobinHost([host], hostBookings, candidateStart, 30, 0, 0);

        selected.Should().BeNull();
    }

    [Fact]
    public void SelectRoundRobinHost_WhenTwoHostsEqualLoad_ShouldPickLessRecentlyBooked()
    {
        var host1 = CreateRotatingHost();
        var host2 = CreateRotatingHost();
        var candidateStart = new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero);

        // Both have 1 booking each but host1's was more recent (host2 waited longer)
        var recentBooking = new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero);
        var olderBooking = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = [CreateBooking(host1.UserId, recentBooking, recentBooking.AddMinutes(30))],
            [host2.UserId] = [CreateBooking(host2.UserId, olderBooking, olderBooking.AddMinutes(30))]
        };

        var selected = _calculator.SelectRoundRobinHost([host1, host2], hostBookings, candidateStart, 30, 0, 0);

        // host2 had an older last booking so should be selected (waited longer)
        selected.Should().Be(host2.UserId);
    }

    [Fact]
    public void SelectRoundRobinHost_WhenOneHostHasMoreBookings_ShouldPickLessLoaded()
    {
        var host1 = CreateRotatingHost();
        var host2 = CreateRotatingHost();
        var candidateStart = new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero);

        // host1 has 2 bookings, host2 has 1 (both weight=100)
        var b1 = CreateBooking(host1.UserId, new DateTimeOffset(2026, 1, 10, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 10, 7, 30, 0, TimeSpan.Zero));
        var b2 = CreateBooking(host1.UserId, new DateTimeOffset(2026, 1, 11, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 11, 7, 30, 0, TimeSpan.Zero));
        var b3 = CreateBooking(host2.UserId, new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 12, 7, 30, 0, TimeSpan.Zero));

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = [b1, b2],
            [host2.UserId] = [b3]
        };

        var selected = _calculator.SelectRoundRobinHost([host1, host2], hostBookings, candidateStart, 30, 0, 0);

        selected.Should().Be(host2.UserId);
    }

    [Fact]
    public void SelectRoundRobinHost_WithPriorityTiers_ShouldAlwaysPickFromTopTier()
    {
        // host1 has priority 0 (top tier), host2 has priority 1 (lower tier)
        var host1 = CreateRotatingHost();
        var host2 = CreateRotatingHost(1);
        var candidateStart = new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero);

        // Give host1 many bookings and host2 zero — host1 should still be selected (higher priority)
        var bookings = Enumerable.Range(0, 5).Select(i =>
            CreateBooking(host1.UserId,
                new DateTimeOffset(2026, 1, 5 + i, 7, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 5 + i, 7, 30, 0, TimeSpan.Zero)
            )
        ).ToArray();

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = bookings
        };

        var selected = _calculator.SelectRoundRobinHost([host1, host2], hostBookings, candidateStart, 30, 0, 0);

        selected.Should().Be(host1.UserId);
    }

    [Fact]
    public void SelectRoundRobinHost_WhenTopPriorityHostBusy_ShouldFallToNextTier()
    {
        var host1 = CreateRotatingHost(); // top tier, busy
        var host2 = CreateRotatingHost(1); // lower tier, free
        var candidateStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = [CreateBooking(host1.UserId, candidateStart, candidateStart.AddMinutes(30))]
        };

        var selected = _calculator.SelectRoundRobinHost([host1, host2], hostBookings, candidateStart, 30, 0, 0);

        selected.Should().Be(host2.UserId);
    }

    // ─── IsSlotAvailable ─────────────────────────────────────────────────────

    [Fact]
    public void IsSlotAvailable_WhenOneRotatingFree_ShouldReturnTrue()
    {
        var host1 = CreateRotatingHost();
        var host2 = CreateRotatingHost();
        var slotStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host1.UserId] = [CreateBooking(host1.UserId, slotStart, slotStart.AddMinutes(30))]
            // host2 is free
        };

        var available = _calculator.IsSlotAvailable(
            _eventType, WorkWeekSchedule, hostBookings, [host1, host2], slotStart, 30, "Africa/Johannesburg"
        );

        available.Should().BeTrue();
    }

    [Fact]
    public void IsSlotAvailable_WhenAllRotatingBusy_ShouldReturnFalse()
    {
        var host = CreateRotatingHost();
        var slotStart = new DateTimeOffset(2026, 1, 12, 7, 0, 0, TimeSpan.Zero);

        var hostBookings = new Dictionary<UserId, Booking[]>
        {
            [host.UserId] = [CreateBooking(host.UserId, slotStart, slotStart.AddMinutes(30))]
        };

        var available = _calculator.IsSlotAvailable(
            _eventType, WorkWeekSchedule, hostBookings, [host], slotStart, 30, "Africa/Johannesburg"
        );

        available.Should().BeFalse();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private Host CreateRotatingHost(int priority = 0, int weight = 100)
    {
        return Host.Create(new TenantId(1), _eventType.Id, UserId.NewId(), false, priority, weight);
    }

    private Host CreateFixedHost()
    {
        return Host.Create(new TenantId(1), _eventType.Id, UserId.NewId(), true, 0, 100);
    }

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
