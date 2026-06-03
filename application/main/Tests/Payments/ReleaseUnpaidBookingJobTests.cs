using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Jobs;
using Main.Features.Scheduling.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using TickerQ.Utilities.Base;
using Xunit;

namespace Main.Tests.Payments;

public sealed class ReleaseUnpaidBookingJobTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_WhenNoExpired_DoesNotCommit()
    {
        var repo = Substitute.For<IBookingRepository>();
        repo.GetExpiredUnpaidBookingsUnfilteredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Booking>());
        var uow = Substitute.For<IUnitOfWork>();
        var job = new ReleaseUnpaidBookingJob(repo, new FakeTimeProvider(Now), uow, NullLogger<ReleaseUnpaidBookingJob>.Instance);

        await job.ExecuteAsync(default!, CancellationToken.None);

        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExpiredFound_ReleasesEachAndCommits()
    {
        var booking = CreateBooking();
        booking.MarkPaymentPending("ref_a", "https://x", Now.AddMinutes(-45));

        var repo = Substitute.For<IBookingRepository>();
        repo.GetExpiredUnpaidBookingsUnfilteredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { booking });
        var uow = Substitute.For<IUnitOfWork>();
        var job = new ReleaseUnpaidBookingJob(repo, new FakeTimeProvider(Now), uow, NullLogger<ReleaseUnpaidBookingJob>.Instance);

        await job.ExecuteAsync(default!, CancellationToken.None);

        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Released);
        booking.Status.Should().Be(BookingStatus.Cancelled);
        repo.Received().Update(booking);
        await uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private static Booking CreateBooking()
        => Booking.Create(
            tenantId: new TenantId(1),
            ownerUserId: UserId.NewId(),
            eventTypeId: EventTypeId.NewId(),
            startTime: Now.AddHours(2),
            durationMinutes: 30,
            beforeEventBufferMinutes: 0,
            afterEventBufferMinutes: 0,
            bookerName: "Alice",
            bookerEmail: "alice@example.com",
            timeZone: "UTC",
            status: BookingStatus.Accepted,
            responses: new Dictionary<string, string>()
        );

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
