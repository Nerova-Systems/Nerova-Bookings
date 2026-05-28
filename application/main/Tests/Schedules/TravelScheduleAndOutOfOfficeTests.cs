using FluentAssertions;
using Main.Features.Schedules.Domain;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Schedules;

public sealed class TravelScheduleAndOutOfOfficeTests
{
    private static readonly TenantId TenantId = TenantId.NewId();
    private static readonly UserId UserId = UserId.NewId();

    [Fact]
    public void TravelSchedule_Create_WithValidData_ShouldSucceed()
    {
        var travel = TravelSchedule.Create(
            TenantId,
            UserId,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 7),
            "Europe/Paris"
        );

        travel.UserId.Should().Be(UserId);
        travel.TimeZone.Should().Be("Europe/Paris");
        travel.Covers(new DateOnly(2026, 6, 3)).Should().BeTrue();
        travel.Covers(new DateOnly(2026, 6, 8)).Should().BeFalse();
    }

    [Fact]
    public void TravelSchedule_Create_WhenEndDateBeforeStartDate_ShouldThrow()
    {
        var action = () => TravelSchedule.Create(
            TenantId,
            UserId,
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 6, 1),
            "Europe/Paris"
        );

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OutOfOffice_Create_WithValidData_ShouldSucceed()
    {
        var entry = OutOfOffice.Create(
            TenantId,
            UserId,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3)
        );

        entry.UserId.Should().Be(UserId);
        entry.Covers(new DateOnly(2026, 6, 2)).Should().BeTrue();
        entry.Covers(new DateOnly(2026, 6, 4)).Should().BeFalse();
    }

    [Fact]
    public void OutOfOffice_Create_WhenEndDateBeforeStartDate_ShouldThrow()
    {
        var action = () => OutOfOffice.Create(
            TenantId,
            UserId,
            new DateOnly(2026, 6, 5),
            new DateOnly(2026, 6, 1)
        );

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OutOfOffice_Create_WhenToUserIdEqualsUserId_ShouldThrow()
    {
        var action = () => OutOfOffice.Create(
            TenantId,
            UserId,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 3),
            UserId
        );

        action.Should().Throw<ArgumentException>();
    }
}
