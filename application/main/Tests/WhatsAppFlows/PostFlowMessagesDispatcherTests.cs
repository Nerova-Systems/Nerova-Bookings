using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Paystack;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Endpoint;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

/// <summary>
///     Pure-unit tests for <see cref="PostFlowMessagesDispatcher" />: confirmation summary sent,
///     payment link only when timing + subaccount + amount are all set, and infrastructure
///     failures are swallowed not surfaced.
/// </summary>
public sealed class PostFlowMessagesDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantId = new(7);

    [Fact]
    public async Task Handle_WhenBookingMissing_DoesNothing()
    {
        var ctx = new Context();
        ctx.Bookings.GetByIdUnfilteredAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await ctx.Sut.Handle(new BookingCreatedViaFlowEvent(BookingId.NewId(), TenantId, "234801", "Alice"), CancellationToken.None);

        await ctx.WhatsApp.DidNotReceive().SendTextMessageAsync(default!, default!, default!, default!, Arg.Any<CancellationToken>());
        await ctx.Paystack.DidNotReceive().CreatePaymentLinkAsync(default!, default, default!, default!, default!, default, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTimingAfterSession_SendsOnlyConfirmationSummary()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(CreateConfig(PaymentTiming.AfterSession));
        ctx.FlowProfile.GetByTenantId(TenantId, Arg.Any<CancellationToken>()).Returns(CreateProfile("subacc_123"));

        await ctx.Sut.Handle(new BookingCreatedViaFlowEvent(booking.Id, TenantId, "234801", "Alice"), CancellationToken.None);

        await ctx.WhatsApp.Received(1).SendTextMessageAsync("phone_id", "tok", "234801", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ctx.Paystack.DidNotReceive().CreatePaymentLinkAsync(default!, default, default!, default!, default!, default, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTimingDepositAndSubaccountSet_CreatesPaymentLinkAndSendsBothMessages()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(CreateConfig(PaymentTiming.Deposit, depositCents: 500_000));
        ctx.FlowProfile.GetByTenantId(TenantId, Arg.Any<CancellationToken>()).Returns(CreateProfile("subacc_123"));
        ctx.Paystack
            .CreatePaymentLinkAsync(
                "subacc_123",
                500_000,
                "NGN",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PaystackPaymentLink("https://checkout.paystack.com/abc", "access_42", "ref_42"));

        await ctx.Sut.Handle(new BookingCreatedViaFlowEvent(booking.Id, TenantId, "234801", "Alice"), CancellationToken.None);

        booking.PaymentStatus.Should().Be(BookingPaymentStatus.Pending);
        booking.PaymentReference.Should().Be("ref_42");
        booking.PaymentLinkUrl.Should().Be("https://checkout.paystack.com/abc");
        ctx.Bookings.Received().Update(booking);
        await ctx.UnitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        // Two messages: confirmation summary + payment link
        await ctx.WhatsApp.Received(2).SendTextMessageAsync("phone_id", "tok", "234801", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSubaccountMissing_SkipsPaymentButStillSendsConfirmation()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(CreateConfig(PaymentTiming.Deposit, depositCents: 1000));
        ctx.FlowProfile.GetByTenantId(TenantId, Arg.Any<CancellationToken>()).Returns(CreateProfile(paystackSubaccountCode: null));

        await ctx.Sut.Handle(new BookingCreatedViaFlowEvent(booking.Id, TenantId, "234801", "Alice"), CancellationToken.None);

        booking.PaymentStatus.Should().Be(BookingPaymentStatus.NotRequired);
        await ctx.WhatsApp.Received(1).SendTextMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ctx.Paystack.DidNotReceive().CreatePaymentLinkAsync(default!, default, default!, default!, default!, default, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWhatsAppThrows_SwallowsAndDoesNotPropagate()
    {
        var ctx = new Context();
        var booking = CreateBooking();
        ctx.Bookings.GetByIdUnfilteredAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        ctx.Config.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(CreateConfig(PaymentTiming.AfterSession));
        ctx.FlowProfile.GetByTenantId(TenantId, Arg.Any<CancellationToken>()).Returns(CreateProfile("subacc"));
        ctx.WhatsApp.SendTextMessageAsync(default!, default!, default!, default!, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs<Task>(_ => throw new HttpRequestException("boom"));

        var act = () => ctx.Sut.Handle(new BookingCreatedViaFlowEvent(booking.Id, TenantId, "234801", "Alice"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ─── fixtures ─────────────────────────────────────────────────────────

    private static Booking CreateBooking()
        => Booking.Create(
            tenantId: TenantId,
            ownerUserId: UserId.NewId(),
            eventTypeId: EventTypeId.NewId(),
            startTime: Now.AddHours(3),
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
        var cfg = TenantFlowConfig.Create(TenantId, BusinessVertical.HairSalon);
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
        return cfg;
    }

    private static WhatsAppFlowProfile CreateProfile(string? paystackSubaccountCode)
        => new(
            TenantId: TenantId,
            WabaId: "waba_1",
            PhoneNumberId: "phone_id",
            DisplayPhoneNumber: "+234801",
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
        public PostFlowMessagesDispatcher Sut { get; }

        public Context()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            Sut = new PostFlowMessagesDispatcher(
                Bookings, Config, FlowProfile, WhatsApp, Paystack, UnitOfWork,
                new FakeTimeProvider(Now), config, NullLogger<PostFlowMessagesDispatcher>.Instance
            );
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
