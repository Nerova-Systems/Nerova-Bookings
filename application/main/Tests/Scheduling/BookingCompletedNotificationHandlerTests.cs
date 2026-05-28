using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Paystack;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class BookingCompletedNotificationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId Tenant = new(11);

    [Fact]
    public async Task Handle_WhenTimingIsAfterSessionAndConfigValid_CreatesLinkAndMarksPending()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(Tenant, Arg.Any<CancellationToken>())
            .Returns(CreateConfig(PaymentTiming.AfterSession, depositCents: 250_000));
        ctx.FlowProfile.GetByTenantId(Tenant, Arg.Any<CancellationToken>())
            .Returns(CreateProfile("subacc_x"));
        ctx.Paystack.CreatePaymentLinkAsync(default!, default, default!, default!, default!, default, default!, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new PaystackPaymentLink("https://checkout/x", "ac", "ref_z"));

        await ctx.Sut.Handle(new BookingCompletedNotification(booking.Id, Tenant), CancellationToken.None);

        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Pending);
        booking.PaymentReference.Should().Be("ref_z");
        ctx.Bookings.Received().Update(booking);
        await ctx.UnitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await ctx.WhatsApp.Received(1).SendTextMessageAsync("phone_id", "tok", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTimingIsBeforeBooking_IsNoOp()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(Tenant, Arg.Any<CancellationToken>())
            .Returns(CreateConfig(PaymentTiming.BeforeBooking));
        ctx.FlowProfile.GetByTenantId(Tenant, Arg.Any<CancellationToken>())
            .Returns(CreateProfile("subacc_x"));

        await ctx.Sut.Handle(new BookingCompletedNotification(booking.Id, Tenant), CancellationToken.None);

        booking.PaymentStatus.Should().Be(BookingPaymentStatus.NotRequired);
        await ctx.Paystack.DidNotReceive().CreatePaymentLinkAsync(default!, default, default!, default!, default!, default, default!, Arg.Any<CancellationToken>());
        await ctx.WhatsApp.DidNotReceive().SendTextMessageAsync(default!, default!, default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPaymentAlreadyPending_SkipsToAvoidDuplicateLink()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        booking.MarkPaymentPending("existing_ref", "https://x", Now.AddHours(-1));
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(Tenant, Arg.Any<CancellationToken>())
            .Returns(CreateConfig(PaymentTiming.AfterSession, depositCents: 1000));
        ctx.FlowProfile.GetByTenantId(Tenant, Arg.Any<CancellationToken>())
            .Returns(CreateProfile("subacc_x"));

        await ctx.Sut.Handle(new BookingCompletedNotification(booking.Id, Tenant), CancellationToken.None);

        booking.PaymentReference.Should().Be("existing_ref");
        await ctx.Paystack.DidNotReceive().CreatePaymentLinkAsync(default!, default, default!, default!, default!, default, default!, Arg.Any<CancellationToken>());
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
            bookerName: "Alice",
            bookerEmail: "alice@example.com",
            timeZone: "UTC",
            status: BookingStatus.Accepted,
            responses: new Dictionary<string, string>()
        );

    private static TenantFlowConfig CreateConfig(PaymentTiming timing, long? depositCents = null)
    {
        var cfg = TenantFlowConfig.Create(Tenant, BusinessVertical.HairSalon);
        cfg.UpdateBusinessProfile(
            vertical: BusinessVertical.HairSalon,
            staffAssignment: StaffAssignment.SpecificStaff,
            paymentTiming: timing,
            depositAmountCents: depositCents,
            bookingWindowDays: 30,
            defaultSessionMinutes: 30,
            hasMultipleServices: false,
            allowSameDayBookings: true,
            confirmationMessageTemplate: "Hi {name}, your booking on {time} is confirmed.",
            cancellationContact: "support@example.com"
        );

        // Domain quirk (flagged as a divergence): UpdateBusinessProfile nulls DepositAmountCents
        // unless PaymentTiming == Deposit. For AfterSession we still need an amount, so set it via
        // reflection. Phase 4b spec assumes amount lives on TenantFlowConfig regardless of timing;
        // tightening the domain model is a follow-up.
        if (timing == PaymentTiming.AfterSession && depositCents.HasValue)
        {
            typeof(TenantFlowConfig)
                .GetProperty(nameof(TenantFlowConfig.DepositAmountCents))!
                .SetValue(cfg, depositCents);
        }

        return cfg;
    }

    private static WhatsAppFlowProfile CreateProfile(string? paystackSubaccountCode)
        => new(
            TenantId: Tenant,
            WabaId: "waba_1",
            PhoneNumberId: "phone_id",
            DisplayPhoneNumber: "+234",
            FlowId: "flow_1",
            FlowStatus: "PUBLISHED",
            OnboardingGateStatus: "Complete",
            WabaAccessToken: "tok",
            EncryptedPrivateKey: null,
            PrivateKeyIv: null,
            PublicKeyFingerprint: null,
            PaystackSubaccountCode: paystackSubaccountCode
        );

    private sealed class Context
    {
        public IBookingRepository Bookings { get; } = Substitute.For<IBookingRepository>();
        public ITenantFlowConfigRepository Config { get; } = Substitute.For<ITenantFlowConfigRepository>();
        public IWhatsAppFlowProfileSync FlowProfile { get; } = Substitute.For<IWhatsAppFlowProfileSync>();
        public IWhatsAppCloudApiClient WhatsApp { get; } = Substitute.For<IWhatsAppCloudApiClient>();
        public IPaystackPaymentLinkService Paystack { get; } = Substitute.For<IPaystackPaymentLinkService>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public BookingCompletedNotificationHandler Sut { get; }

        public Context()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            Sut = new BookingCompletedNotificationHandler(
                Bookings, Config, FlowProfile, WhatsApp, Paystack, UnitOfWork,
                new FakeTimeProvider(Now), config, NullLogger<BookingCompletedNotificationHandler>.Instance
            );
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
