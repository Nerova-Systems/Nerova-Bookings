using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Api.Endpoints;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Main.Tests;

public sealed class PaystackPaymentsEndpointTests : EndpointBaseTest<MainDbContext>
{
    private static FakePaystackClient PaystackClient { get; set; } = new();
    private static FakeTwilioVerifyClient TwilioVerifyClient { get; set; } = new();

    [Fact]
    public async Task SavePaystackSubaccount_WhenNew_ShouldCreateAndStoreMaskedAccountNumber()
    {
        PaystackClient = new FakePaystackClient();
        await SeedShellAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/subaccount", NewSubaccountRequest("1234567890"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subaccount = await ReadSubaccountAsync();
        subaccount.SubaccountCode.Should().Be("ACCT_test");
        subaccount.BusinessName.Should().Be("Tenant 1");
        subaccount.MaskedAccountNumber.Should().Be("**** 7890");
        subaccount.AccountName.Should().Be("Sea Point Studio");
        PaystackClient.CreateCalls.Should().Be(1);
    }

    [Fact]
    public async Task SavePaystackSubaccount_WhenExisting_ShouldUpdateExistingSubaccount()
    {
        PaystackClient = new FakePaystackClient();
        await SeedShellAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/subaccount", NewSubaccountRequest("1234567890"));

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/subaccount", NewSubaccountRequest("9876543210"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subaccount = await ReadSubaccountAsync();
        subaccount.MaskedAccountNumber.Should().Be("**** 3210");
        PaystackClient.UpdateCalls.Should().Be(1);
    }

    [Fact]
    public async Task InitializePayment_WhenNoSubaccount_ShouldReturnBadRequest()
    {
        PaystackClient = new FakePaystackClient();
        await SeedShellAsync();
        var appointmentId = await ReadDepositAppointmentIdAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/initialize", new { appointmentId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Connect Paystack payouts before accepting appointment payments.");
    }

    [Fact]
    public async Task InitializePayment_WhenSubaccountExists_ShouldSendSubaccountToPaystack()
    {
        PaystackClient = new FakePaystackClient();
        await SeedShellAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/subaccount", NewSubaccountRequest("1234567890"));
        var appointmentId = await ReadDepositAppointmentIdAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/initialize", new { appointmentId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PaystackClient.LastTransactionRequest?.SubaccountCode.Should().Be("ACCT_test");
    }

    [Fact]
    public async Task CreatePublicAppointment_WhenServiceRequiresFullPaymentBeforeBooking_ShouldInitializeFullPricePayment()
    {
        PaystackClient = new FakePaystackClient();
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        await SeedShellAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/subaccount", NewSubaccountRequest("1234567890"));
        var service = await UpdateServicePaymentPolicyAsync("Express session", ServicePaymentPolicy.FullPaymentBeforeBooking, 0);

        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/main/public-booking/sea-point-studio/appointments", await VerifiedPublicBookingRequestAsync(service.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PublicBookingCreatedResponse>();
        body!.PaymentRequired.Should().BeTrue();
        body.PaymentUrl.Should().Be("https://checkout.paystack.test/reference");
        PaystackClient.LastTransactionRequest?.AmountCents.Should().Be(service.PriceCents);
    }

    [Fact]
    public async Task CreatePublicAppointment_WhenServiceCollectsAfterAppointment_ShouldSkipCheckoutAndTrackPendingPayment()
    {
        PaystackClient = new FakePaystackClient();
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        await SeedShellAsync();
        var service = await UpdateServicePaymentPolicyAsync("Express session", ServicePaymentPolicy.CollectAfterAppointment, 0);

        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/main/public-booking/sea-point-studio/appointments", await VerifiedPublicBookingRequestAsync(service.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PublicBookingCreatedResponse>();
        body!.PaymentRequired.Should().BeFalse();
        body.PaymentUrl.Should().BeNull();
        PaystackClient.LastTransactionRequest.Should().BeNull();
        var appointment = await ReadAppointmentByReferenceAsync(body.Reference);
        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        appointment.PaymentStatus.Should().Be(AppointmentPaymentStatus.Pending);
    }

    [Fact]
    public async Task CreateTerminalIntent_WhenPaystackConfigured_ShouldCreateSplitTerminalAndAppointmentIntent()
    {
        PaystackClient = new FakePaystackClient();
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        await SeedShellAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/payments/paystack/subaccount", NewSubaccountRequest("1234567890"));
        var service = await UpdateServicePaymentPolicyAsync("Express session", ServicePaymentPolicy.CollectAfterAppointment, 0);
        var bookingResponse = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/appointments",
            await VerifiedPublicBookingRequestAsync(service.Id)
        );
        bookingResponse.EnsureSuccessStatusCode();
        var booking = await bookingResponse.Content.ReadFromJsonAsync<PublicBookingCreatedResponse>();
        var appointmentId = (await ReadAppointmentByReferenceAsync(booking!.Reference)).Id;

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/main/app/appointments/{appointmentId}/payments/terminal-intent", new { });

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        PaystackClient.CreateSplitCalls.Should().Be(1);
        PaystackClient.CreateVirtualTerminalCalls.Should().Be(1);
        PaystackClient.AssignSplitToVirtualTerminalCalls.Should().Be(1);
        var subaccount = await ReadSubaccountAsync();
        subaccount.SplitCode.Should().Be("SPL_test");
        subaccount.VirtualTerminalCode.Should().Be("VT_test");
        var intent = await ReadPaymentIntentAsync(appointmentId);
        intent.Channel.Should().Be(AppointmentPaymentChannel.VirtualTerminal);
        intent.VirtualTerminalCode.Should().Be("VT_test");
        intent.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task UpdateAppointmentStatus_WhenPaymentStatusProvided_ShouldRejectManualPaymentMutation()
    {
        PaystackClient = new FakePaystackClient();
        await SeedShellAsync();
        var appointmentId = await ReadDepositAppointmentIdAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/main/app/appointments/{appointmentId}/status", new { status = "Confirmed", paymentStatus = "Paid" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var appointment = await db.Appointments.IgnoreQueryFilters().SingleAsync(a => a.Id == appointmentId);
        appointment.PaymentStatus.Should().Be(AppointmentPaymentStatus.Pending);
    }

    [Fact]
    public async Task IsTransactionSuccessfulAsync_WhenRequestedAmountMatches_ShouldVerifyPayment()
    {
        var responseBody = """
            {
              "status": true,
              "message": "Verification successful",
              "data": {
                "status": "success",
                "reference": "ps_reference",
                "amount": 40333,
                "requested_amount": 15000,
                "currency": "ZAR"
              }
            }
            """;
        using var httpClient = new HttpClient(new StaticJsonHandler(responseBody));
        var paystackClient = new PaystackClient(new SingleHttpClientFactory(httpClient));
        var previousSecret = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
        Environment.SetEnvironmentVariable("PAYSTACK_SECRET_KEY", "sk_test_unit");
        try
        {
            var result = await paystackClient.IsTransactionSuccessfulAsync("ps_reference", 15000, CancellationToken.None);

            result.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PAYSTACK_SECRET_KEY", previousSecret);
        }
    }

    protected override void RegisterMockLoggers(IServiceCollection services)
    {
        services.RemoveAll<IPaystackClient>();
        services.AddSingleton<IPaystackClient>(_ => PaystackClient);
        services.RemoveAll<ITwilioVerifyClient>();
        services.AddSingleton<ITwilioVerifyClient>(_ => TwilioVerifyClient);
        base.RegisterMockLoggers(services);
    }

    private async Task SeedShellAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell");
        response.EnsureSuccessStatusCode();
    }

    private async Task<PaystackSubaccount> ReadSubaccountAsync()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.PaystackSubaccounts.IgnoreQueryFilters().SingleAsync();
    }

    private async Task<string> ReadDepositAppointmentIdAsync()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var query =
            from appointment in db.Appointments.IgnoreQueryFilters()
            join service in db.BookableServices.IgnoreQueryFilters() on appointment.ServiceId equals service.Id
            where service.DepositCents > 0 && appointment.PaymentStatus == AppointmentPaymentStatus.Pending
            select appointment.Id;
        return await query.FirstAsync();
    }

    private async Task<string> ReadCollectAfterAppointmentIdAsync()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var subaccount = await db.PaystackSubaccounts.IgnoreQueryFilters().SingleAsync();
        var appointment = await db.Appointments.IgnoreQueryFilters().FirstAsync(a => a.TenantId == subaccount.TenantId);
        var service = await db.BookableServices.IgnoreQueryFilters().AsTracking().SingleAsync(s => s.Id == appointment.ServiceId);
        service.PaymentPolicy = ServicePaymentPolicy.CollectAfterAppointment;
        service.DepositCents = 0;
        var version = NewServiceVersion(service, await NextVersionNumberAsync(db, service.Id));
        db.BookableServiceVersions.Add(version);
        appointment.ServiceVersionId = version.Id;
        appointment.PaymentStatus = AppointmentPaymentStatus.Pending;
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task<BookableService> UpdateServicePaymentPolicyAsync(string serviceName, ServicePaymentPolicy paymentPolicy, int depositCents)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var service = await db.BookableServices.IgnoreQueryFilters().AsTracking().SingleAsync(s => s.Name == serviceName);
        service.PaymentPolicy = paymentPolicy;
        service.DepositCents = depositCents;
        db.BookableServiceVersions.Add(NewServiceVersion(service, await NextVersionNumberAsync(db, service.Id)));
        await db.SaveChangesAsync();
        return service;
    }

    private static async Task<int> NextVersionNumberAsync(MainDbContext db, string serviceId)
    {
        var versions = await db.BookableServiceVersions.IgnoreQueryFilters()
            .Where(version => version.ServiceId == serviceId)
            .Select(version => version.VersionNumber)
            .ToListAsync();
        return versions.Count == 0 ? 1 : versions.Max() + 1;
    }

    private static BookableServiceVersion NewServiceVersion(BookableService service, int versionNumber)
    {
        return new BookableServiceVersion
        {
            TenantId = service.TenantId,
            ServiceId = service.Id,
            VersionNumber = versionNumber,
            CategoryId = service.CategoryId,
            Name = service.Name,
            Description = service.Description,
            Mode = service.Mode,
            DurationMinutes = service.DurationMinutes,
            PriceCents = service.PriceCents,
            DepositCents = service.DepositCents,
            PaymentPolicy = service.PaymentPolicy,
            BufferBeforeMinutes = service.BufferBeforeMinutes,
            BufferAfterMinutes = service.BufferAfterMinutes,
            Location = service.Location,
            IsActive = service.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<Appointment> ReadAppointmentByReferenceAsync(string reference)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.Appointments.IgnoreQueryFilters().SingleAsync(a => a.PublicReference == reference);
    }

    private async Task<AppointmentPaymentIntent> ReadPaymentIntentAsync(string appointmentId)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.AppointmentPaymentIntents.IgnoreQueryFilters().SingleAsync(i => i.AppointmentId == appointmentId);
    }

    private static object NewSubaccountRequest(string accountNumber)
    {
        return new
        {
            bankName = "Test Bank",
            bankCode = "632005",
            accountNumber,
            accountName = "Sea Point Studio",
            primaryContactName = "Sarah",
            primaryContactEmail = "sarah@nerovasystems.com",
            primaryContactPhone = "+27820000000"
        };
    }

    private async Task<object> VerifiedPublicBookingRequestAsync(string serviceId)
    {
        var phone = "+27 82 111 0000";
        await AnonymousHttpClient.PostAsJsonAsync("/api/main/public-booking/sea-point-studio/phone-verifications", new { phone });
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/phone-verifications/check",
            new { phone, code = "123456" }
        );
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        return PublicBookingRequest(serviceId, body!["phoneVerificationToken"]!.GetValue<string>());
    }

    private static object PublicBookingRequest(string serviceId, string phoneVerificationToken)
    {
        return new
        {
            serviceId,
            startAt = new DateTimeOffset(2026, 5, 4, 11, 0, 0, TimeSpan.FromHours(2)),
            name = "Terminal Client",
            phone = "+27 82 111 0000",
            email = "terminal@example.com",
            phoneVerificationToken,
            answers = new Dictionary<string, string>()
        };
    }

    private sealed class FakePaystackClient : IPaystackClient
    {
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int CreateSplitCalls { get; private set; }
        public int CreateVirtualTerminalCalls { get; private set; }
        public int AssignSplitToVirtualTerminalCalls { get; private set; }
        public PaystackTransactionRequest? LastTransactionRequest { get; private set; }

        public Task<string?> InitializeTransactionAsync(PaystackTransactionRequest request, CancellationToken cancellationToken)
        {
            LastTransactionRequest = request;
            return Task.FromResult<string?>("https://checkout.paystack.test/reference");
        }

        public Task<bool> IsTransactionSuccessfulAsync(string reference, int amountCents, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<PaystackBankOption>> ListBanksAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PaystackBankOption>>([new PaystackBankOption("Test Bank", "632005", "ZAR", "South Africa")]);
        }

        public Task<PaystackResolvedAccount> ResolveAccountAsync(string bankCode, string accountNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaystackResolvedAccount(accountNumber, "Sea Point Studio"));
        }

        public Task<PaystackSubaccountResult> CreateSubaccountAsync(PaystackSubaccountRequest request, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(ToResult(request));
        }

        public Task<PaystackSubaccountResult> UpdateSubaccountAsync(string subaccountCode, PaystackSubaccountRequest request, CancellationToken cancellationToken)
        {
            UpdateCalls++;
            return Task.FromResult(ToResult(request));
        }

        public Task<IReadOnlyList<PaystackSettlementResult>> ListSettlementsAsync(string subaccountCode, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PaystackSettlementResult>>([]);
        }

        public Task<PaystackSplitResult> CreateSplitAsync(PaystackSplitRequest request, CancellationToken cancellationToken)
        {
            CreateSplitCalls++;
            return Task.FromResult(new PaystackSplitResult("SPL_test"));
        }

        public Task<PaystackVirtualTerminalResult> CreateVirtualTerminalAsync(PaystackVirtualTerminalRequest request, CancellationToken cancellationToken)
        {
            CreateVirtualTerminalCalls++;
            return Task.FromResult(new PaystackVirtualTerminalResult("VT_test", request.Name, true, "ZAR"));
        }

        public Task AssignSplitToVirtualTerminalAsync(string terminalCode, string splitCode, CancellationToken cancellationToken)
        {
            AssignSplitToVirtualTerminalCalls++;
            return Task.CompletedTask;
        }

        private static PaystackSubaccountResult ToResult(PaystackSubaccountRequest request)
        {
            return new PaystackSubaccountResult("ACCT_test", 123, request.BusinessName, request.BankName, request.BankCode, request.AccountName, request.AccountNumber, "ZAR", true, true, "auto");
        }
    }

    private sealed class FakeTwilioVerifyClient : ITwilioVerifyClient
    {
        public Task<TwilioVerificationStarted> StartVerificationAsync(string phone, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TwilioVerificationStarted("VE_test", "pending"));
        }

        public Task<bool> CheckVerificationAsync(string phone, string code, CancellationToken cancellationToken)
        {
            return Task.FromResult(code == "123456");
        }
    }

    private sealed class SingleHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return httpClient;
        }
    }

    private sealed class StaticJsonHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
