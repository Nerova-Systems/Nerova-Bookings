using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Jobs;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using Xunit;

namespace Main.Tests.Payments;

public sealed class SendPaymentReminderJobTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly TenantId Tenant = new(7);

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingPayments_DoesNotCommit()
    {
        var repo = Substitute.For<IBookingRepository>();
        repo.GetPendingPaymentsForReminderUnfilteredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Booking>());
        var profileSync = Substitute.For<IWhatsAppFlowProfileSync>();
        var whatsApp = Substitute.For<IWhatsAppCloudApiClient>();
        var uow = Substitute.For<IUnitOfWork>();

        var job = new SendPaymentReminderJob(repo, profileSync, whatsApp, new FakeTimeProvider(Now), uow, NullLogger<SendPaymentReminderJob>.Instance);
        await job.ExecuteAsync(default!, CancellationToken.None);

        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await whatsApp.DidNotReceive().SendTextMessageAsync(default!, default!, default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPendingFound_SendsReminderAndStampsTimestamp()
    {
        var booking = CreateBooking();
        booking.MarkPaymentPending("ref_z", "https://checkout/x", Now.AddHours(-50));

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPendingPaymentsForReminderUnfilteredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { booking });

        var profileSync = Substitute.For<IWhatsAppFlowProfileSync>();
        profileSync.GetByTenantId(Tenant, Arg.Any<CancellationToken>())
            .Returns(new WhatsAppFlowProfile(Tenant, "waba_1", "phone_id", "+234", "flow_1", "PUBLISHED", "Complete", "tok", null, null, null, "subacc"));

        var whatsApp = Substitute.For<IWhatsAppCloudApiClient>();
        var uow = Substitute.For<IUnitOfWork>();

        var job = new SendPaymentReminderJob(repo, profileSync, whatsApp, new FakeTimeProvider(Now), uow, NullLogger<SendPaymentReminderJob>.Instance);
        await job.ExecuteAsync(default!, CancellationToken.None);

        booking.PaymentReminderSentAt.Should().Be(Now);
        await whatsApp.Received(1).SendTextMessageAsync("phone_id", "tok", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        repo.Received().Update(booking);
        await uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private static Booking CreateBooking()
        => Booking.Create(
            tenantId: Tenant,
            ownerUserId: UserId.NewId(),
            eventTypeId: EventTypeId.NewId(),
            startTime: Now.AddHours(2),
            durationMinutes: 30,
            beforeEventBufferMinutes: 0,
            afterEventBufferMinutes: 0,
            bookerName: "Bob",
            bookerEmail: "bob@example.com",
            timeZone: "UTC",
            status: BookingStatus.Accepted,
            responses: new Dictionary<string, string>()
        );

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
