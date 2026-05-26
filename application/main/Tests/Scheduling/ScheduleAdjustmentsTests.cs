using FluentAssertions;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class ScheduleAdjustmentsTests
{
    private static readonly TenantId TenantId = TenantId.NewId();
    private static readonly UserId UserId = UserId.NewId();
    private static readonly TimeZoneInfo DefaultZone = TimeZoneInfo.FindSystemTimeZoneById("UTC");

    [Fact]
    public void Empty_ReturnsDefaultTimeZoneAndNeverOutOfOffice()
    {
        ScheduleAdjustments.Empty.GetEffectiveTimeZone(new DateOnly(2026, 6, 1), DefaultZone).Id.Should().Be("UTC");
        ScheduleAdjustments.Empty.IsOutOfOffice(new DateOnly(2026, 6, 1)).Should().BeFalse();
    }

    [Fact]
    public void GetEffectiveTimeZone_WhenTravelCoversDate_ReturnsTravelTimeZone()
    {
        var travel = TravelSchedule.Create(TenantId, UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7), "Europe/Paris");
        var adjustments = new ScheduleAdjustments([travel], []);

        adjustments.GetEffectiveTimeZone(new DateOnly(2026, 6, 3), DefaultZone).Id.Should().Be("Europe/Paris");
        adjustments.GetEffectiveTimeZone(new DateOnly(2026, 6, 8), DefaultZone).Id.Should().Be("UTC");
    }

    [Fact]
    public void GetEffectiveTimeZone_WhenTravelTimeZoneInvalid_FallsBackToDefault()
    {
        var travel = TravelSchedule.Create(TenantId, UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7), "Not/A/Zone");
        var adjustments = new ScheduleAdjustments([travel], []);

        adjustments.GetEffectiveTimeZone(new DateOnly(2026, 6, 3), DefaultZone).Id.Should().Be("UTC");
    }

    [Fact]
    public void IsOutOfOffice_WhenAnyEntryCoversDate_ReturnsTrue()
    {
        var entry = OutOfOffice.Create(TenantId, UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3));
        var adjustments = new ScheduleAdjustments([], [entry]);

        adjustments.IsOutOfOffice(new DateOnly(2026, 6, 1)).Should().BeTrue();
        adjustments.IsOutOfOffice(new DateOnly(2026, 6, 3)).Should().BeTrue();
        adjustments.IsOutOfOffice(new DateOnly(2026, 6, 4)).Should().BeFalse();
    }
}
