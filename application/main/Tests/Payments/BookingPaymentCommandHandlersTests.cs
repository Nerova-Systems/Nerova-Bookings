using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Commands;
using Main.Features.Payments.Domain;
using Main.Features.Payments.Infrastructure;
using Main.Features.Scheduling.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Payments;

public sealed class BookingPaymentCommandHandlersTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConfirmBookingPayment_WhenEventAlreadyProcessed_IsNoOp()
    {
        var processed = Substitute.For<IProcessedPaymentEventRepository>();
        processed.IsProcessedAsync("evt_1", Arg.Any<CancellationToken>()).Returns(true);
        var bookings = Substitute.For<IBookingRepository>();

        var handler = new ConfirmBookingPaymentHandler(bookings, processed, new FakeTimeProvider(Now), NullLogger<ConfirmBookingPaymentHandler>.Instance);
        var result = await handler.Handle(new ConfirmBookingPaymentCommand("ref_42", "evt_1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await bookings.DidNotReceive().GetByPaymentReferenceUnfilteredAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await processed.DidNotReceive().AddAsync(Arg.Any<ProcessedPaymentEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmBookingPayment_WhenBookingExists_MarksPaidAndRecordsEvent()
    {
        var booking = CreateBooking();
        booking.MarkPaymentPending("ref_42", "https://checkout/abc", Now.AddMinutes(-5));

        var bookings = Substitute.For<IBookingRepository>();
        bookings.GetByPaymentReferenceUnfilteredAsync("ref_42", Arg.Any<CancellationToken>()).Returns(booking);
        var processed = Substitute.For<IProcessedPaymentEventRepository>();
        processed.IsProcessedAsync("evt_2", Arg.Any<CancellationToken>()).Returns(false);

        var handler = new ConfirmBookingPaymentHandler(bookings, processed, new FakeTimeProvider(Now), NullLogger<ConfirmBookingPaymentHandler>.Instance);
        var result = await handler.Handle(new ConfirmBookingPaymentCommand("ref_42", "evt_2"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Paid);
        booking.PaymentStateChangedAt.Should().Be(Now);
        bookings.Received().Update(booking);
        await processed.Received().AddAsync(Arg.Is<ProcessedPaymentEvent>(e => e.EventId == "evt_2"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmBookingPayment_WhenBookingMissing_StillRecordsEventToStopRetries()
    {
        var bookings = Substitute.For<IBookingRepository>();
        bookings.GetByPaymentReferenceUnfilteredAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);
        var processed = Substitute.For<IProcessedPaymentEventRepository>();

        var handler = new ConfirmBookingPaymentHandler(bookings, processed, new FakeTimeProvider(Now), NullLogger<ConfirmBookingPaymentHandler>.Instance);
        var result = await handler.Handle(new ConfirmBookingPaymentCommand("missing_ref", "evt_3"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await processed.Received().AddAsync(Arg.Is<ProcessedPaymentEvent>(e => e.EventId == "evt_3"), Arg.Any<CancellationToken>());
        bookings.DidNotReceive().Update(Arg.Any<Booking>());
    }

    [Fact]
    public async Task ReleaseBookingPayment_WhenBookingExists_MarksFailedAndReleasesSlot()
    {
        var booking = CreateBooking();
        booking.MarkPaymentPending("ref_77", "https://checkout/xyz", Now.AddMinutes(-30));

        var bookings = Substitute.For<IBookingRepository>();
        bookings.GetByPaymentReferenceUnfilteredAsync("ref_77", Arg.Any<CancellationToken>()).Returns(booking);
        var processed = Substitute.For<IProcessedPaymentEventRepository>();

        var handler = new ReleaseBookingPaymentHandler(bookings, processed, new FakeTimeProvider(Now), NullLogger<ReleaseBookingPaymentHandler>.Instance);
        var result = await handler.Handle(new ReleaseBookingPaymentCommand("ref_77", "evt_4"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Released);
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().NotBeNullOrWhiteSpace();
        await processed.Received().AddAsync(Arg.Is<ProcessedPaymentEvent>(e => e.EventId == "evt_4"), Arg.Any<CancellationToken>());
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
