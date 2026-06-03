using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Permissions.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class MarkBookingCompletedCommandTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly TenantId Tenant = new(3);
    private static readonly UserId Owner = UserId.NewId();

    [Fact]
    public async Task Handle_WhenBookingExists_MarksCompletedAndPublishesNotification()
    {
        var ctx = new Context(SystemRoles.Owner);
        var booking = CreateBooking();
        ctx.Bookings.GetByIdInTenantWithEventTypeAsync(Tenant, booking.Id, Arg.Any<CancellationToken>())
            .Returns(new BookingWithEventType(booking, CreateEventType()));

        var result = await ctx.Sut.Handle(new MarkBookingCompletedCommand(booking.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        ctx.Bookings.Received().Update(booking);
        await ctx.Publisher.Received(1).Publish(Arg.Is<BookingCompletedNotification>(n => n.BookingId == booking.Id && n.TenantId == Tenant), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyCompleted_ReturnsBadRequest()
    {
        var ctx = new Context(SystemRoles.Owner);
        var booking = CreateBooking();
        booking.MarkCompleted();
        ctx.Bookings.GetByIdInTenantWithEventTypeAsync(Tenant, booking.Id, Arg.Any<CancellationToken>())
            .Returns(new BookingWithEventType(booking, CreateEventType()));

        var result = await ctx.Sut.Handle(new MarkBookingCompletedCommand(booking.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await ctx.Publisher.DidNotReceive().Publish(Arg.Any<BookingCompletedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMemberHostsBooking_Succeeds()
    {
        var ctx = new Context(SystemRoles.Member);
        var booking = CreateBooking();
        ctx.Bookings.GetForOwnerWithEventTypeAsync(Tenant, Owner, Arg.Any<TenantId?>(), booking.Id, Arg.Any<CancellationToken>())
            .Returns(new BookingWithEventType(booking, CreateEventType()));

        var result = await ctx.Sut.Handle(new MarkBookingCompletedCommand(booking.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static Booking CreateBooking()
    {
        return Booking.Create(
            Tenant,
            Owner,
            EventTypeId.NewId(),
            Now.AddHours(-1),
            30,
            0,
            0,
            "Alice",
            "alice@example.com",
            "UTC",
            BookingStatus.Accepted,
            new Dictionary<string, string>()
        );
    }

    private static EventType CreateEventType()
    {
        return EventType.Create(Tenant, Owner, "Hair", "hair", null, 30, false, ScheduleId.NewId(), 0, 0, 0, 0, null, null, null);
    }

    private sealed class Context
    {
        public Context(string role)
        {
            Execution.UserInfo.Returns(new UserInfo { TenantId = Tenant, Id = Owner, Role = role, IsAuthenticated = true });
            Execution.ActiveTeamId.Returns((TenantId?)null);
            Sut = new MarkBookingCompletedHandler(Bookings, History, Execution, new FakeTimeProvider(Now), Publisher);
        }

        public IBookingRepository Bookings { get; } = Substitute.For<IBookingRepository>();

        private IBookingHistoryEntryRepository History { get; } = Substitute.For<IBookingHistoryEntryRepository>();

        private IExecutionContext Execution { get; } = Substitute.For<IExecutionContext>();

        public IPublisher Publisher { get; } = Substitute.For<IPublisher>();

        public MarkBookingCompletedHandler Sut { get; }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
