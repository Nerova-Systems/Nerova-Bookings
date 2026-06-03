using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Jobs;
using Main.Features.Scheduling.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Persistence;
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

        await job.ExecuteAsync(null!, CancellationToken.None);

        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExpiredFound_ReleasesEachAndCommits()
    {
        var booking = CreateBooking();
        booking.MarkPaymentPending("ref_a", "https://x", Now.AddMinutes(-45));

        var repo = Substitute.For<IBookingRepository>();
        repo.GetExpiredUnpaidBookingsUnfilteredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([booking]);
        var uow = Substitute.For<IUnitOfWork>();
        var job = new ReleaseUnpaidBookingJob(repo, new FakeTimeProvider(Now), uow, NullLogger<ReleaseUnpaidBookingJob>.Instance);

        await job.ExecuteAsync(null!, CancellationToken.None);

        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Released);
        booking.Status.Should().Be(BookingStatus.Cancelled);
        repo.Received().Update(booking);
        await uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private static Booking CreateBooking()
    {
        return Booking.Create(
            new TenantId(1),
            UserId.NewId(),
            EventTypeId.NewId(),
            Now.AddHours(2),
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

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
