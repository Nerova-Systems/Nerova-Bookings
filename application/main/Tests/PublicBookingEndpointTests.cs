using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Main.Tests;

public sealed class PublicBookingEndpointTests : EndpointBaseTest<MainDbContext>
{
    private static FakeTwilioVerifyClient TwilioVerifyClient { get; set; } = new();

    [Fact]
    public async Task PublicProfile_ShouldReturnBusinessBranding()
    {
        var profile = await ReadPublicProfileAsync();

        profile["name"]!.GetValue<string>().Should().Be("Sea Point studio");
        profile["logoUrl"]!.GetValue<string>().Should().Be("/logos/sea-point-studio.svg");
    }

    [Fact]
    public async Task ClientPrefill_WithoutVerifiedPhone_ShouldNotReturnClientData()
    {
        await ReadPublicProfileAsync();

        var response = await AnonymousHttpClient.GetAsync("/api/main/public-booking/sea-point-studio/client-prefill?phone=%2B27823417890");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Liam Botha");
        body.Should().NotContain("liam@example.com");
    }

    [Fact]
    public async Task StartPhoneVerification_ShouldNormalizePhoneAndSendTwilioOtp()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        await ReadPublicProfileAsync();

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/phone-verifications",
            new { phone = "082 341 7890" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body.Should().NotBeNull();
        body!["maskedPhone"]!.GetValue<string>().Should().Contain("7890");
        TwilioVerifyClient.LastStartedPhone.Should().Be("+27823417890");
    }

    [Fact]
    public async Task StartPhoneVerification_WhenTwilioAuthenticationFails_ShouldReturnUsefulConfigurationError()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient { ShouldThrowAuthenticationError = true };
        await ReadPublicProfileAsync();

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/phone-verifications",
            new { phone = "082 341 7890" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SMS verification is not authenticated.");
        body.Should().NotContain("\"code\":20003");
    }

    [Fact]
    public async Task CheckPhoneVerification_WhenApproved_ShouldReturnSafePrefillForExistingClient()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        await StartPhoneVerificationAsync("082 341 7890");

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/phone-verifications/check",
            new { phone = "+27 82 341 7890", code = "123456" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body.Should().NotBeNull();
        body!["phoneVerificationToken"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        body["name"]!.GetValue<string>().Should().Be("Liam Botha");
        body["email"]!.GetValue<string>().Should().Be("liam@example.com");
    }

    [Fact]
    public async Task PublicBooking_WithoutVerifiedPhoneToken_ShouldBeRejected()
    {
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        var slots = await AnonymousHttpClient.GetFromJsonAsync<JsonArray>($"/api/main/public-booking/sea-point-studio/slots?serviceId={serviceId}&date=2026-04-27");
        var startAt = slots![0]!["startAt"]!.GetValue<DateTimeOffset>();

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/appointments",
            new
            {
                serviceId,
                startAt,
                name = "Changed Public Name",
                phone = "+27823417890",
                email = "changed@example.com",
                answers = new Dictionary<string, string> { ["note"] = "Public booking note" }
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Verify your phone number before booking.");
    }

    [Fact]
    public async Task PublicBooking_WithVerifiedExistingPhone_ShouldReuseClientWithoutOverwritingClientDetails()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        var phoneVerificationToken = await VerifyPhoneAsync("082 341 7890");
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        var slots = await AnonymousHttpClient.GetFromJsonAsync<JsonArray>($"/api/main/public-booking/sea-point-studio/slots?serviceId={serviceId}&date=2026-04-27");
        var startAt = slots![0]!["startAt"]!.GetValue<DateTimeOffset>();

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/appointments",
            new
            {
                serviceId,
                startAt,
                name = "Changed Public Name",
                phone = "082 341 7890",
                email = "changed@example.com",
                phoneVerificationToken,
                answers = new Dictionary<string, string> { ["note"] = "Public booking note" }
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var clients = await db.Clients.IgnoreQueryFilters().Where(client => client.Name.Contains("Liam") || client.Name == "Changed Public Name").ToListAsync();
        clients.Should().ContainSingle();
        clients[0].Name.Should().Be("Liam Botha");
        clients[0].Email.Should().Be("liam@example.com");
        clients[0].Phone.Should().Be("+27823417890");
    }

    [Fact]
    public async Task PublicBooking_WithMismatchedVerifiedPhone_ShouldBeRejected()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        var phoneVerificationToken = await VerifyPhoneAsync("082 341 7890");
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        var slots = await AnonymousHttpClient.GetFromJsonAsync<JsonArray>($"/api/main/public-booking/sea-point-studio/slots?serviceId={serviceId}&date=2026-04-27");
        var startAt = slots![0]!["startAt"]!.GetValue<DateTimeOffset>();

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/appointments",
            new
            {
                serviceId,
                startAt,
                name = "Different Person",
                phone = "+27 99 999 9999",
                email = "different@example.com",
                phoneVerificationToken,
                answers = new Dictionary<string, string>()
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PublicBooking_WithReusedVerifiedPhoneToken_ShouldBeRejected()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        var phoneVerificationToken = await VerifyPhoneAsync("+27 82 555 0000");
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        var slots = await AnonymousHttpClient.GetFromJsonAsync<JsonArray>($"/api/main/public-booking/sea-point-studio/slots?serviceId={serviceId}&date=2026-04-27");

        var first = await CreateVerifiedBookingAsync(serviceId, slots![0]!["startAt"]!.GetValue<DateTimeOffset>(), "+27 82 555 0000", phoneVerificationToken);
        var second = await CreateVerifiedBookingAsync(serviceId, slots[1]!["startAt"]!.GetValue<DateTimeOffset>(), "+27 82 555 0000", phoneVerificationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PublicBooking_ShouldAttachLatestServiceVersion()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        await UpdatePublicServiceVersionAsync(serviceId);
        var phoneVerificationToken = await VerifyPhoneAsync("+27 82 555 0101");
        var slots = await AnonymousHttpClient.GetFromJsonAsync<JsonArray>($"/api/main/public-booking/sea-point-studio/slots?serviceId={serviceId}&date=2026-04-27");

        var response = await CreateVerifiedBookingAsync(serviceId, slots![0]!["startAt"]!.GetValue<DateTimeOffset>(), "+27 82 555 0101", phoneVerificationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var appointment = await db.Appointments.IgnoreQueryFilters().SingleAsync(appointment => appointment.PublicReference == body!["reference"]!.GetValue<string>());
        var version = await db.BookableServiceVersions.IgnoreQueryFilters().SingleAsync(version => version.Id == appointment.ServiceVersionId);
        version.VersionNumber.Should().Be(2);
        version.Name.Should().Be("Express session v2");
        version.PriceCents.Should().Be(33000);
    }

    [Fact]
    public async Task PublicSlots_WhenManualCalendarBlockOverlaps_ShouldHideBlockedSlots()
    {
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        await AddManualCalendarBlockAsync(
            "Public maintenance",
            new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.FromHours(2))
        );

        var slots = await AnonymousHttpClient.GetFromJsonAsync<JsonArray>(
            $"/api/main/public-booking/sea-point-studio/slots?serviceId={serviceId}&date=2026-04-28"
        );

        slots.Should().NotBeNull();
        slots!.Select(slot => slot!["startAt"]!.GetValue<DateTimeOffset>())
            .Should()
            .NotContain(start => start >= new DateTimeOffset(2026, 4, 28, 9, 30, 0, TimeSpan.FromHours(2)) &&
                                 start < new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public async Task PublicBooking_WhenManualCalendarBlockOverlapsRequestedTime_ShouldReturnConflict()
    {
        TwilioVerifyClient = new FakeTwilioVerifyClient();
        var phoneVerificationToken = await VerifyPhoneAsync("+27 82 555 0202");
        var profile = await ReadPublicProfileAsync();
        var serviceId = profile["services"]!.AsArray().Single(service => service!["name"]!.GetValue<string>() == "Express session")!["id"]!.GetValue<string>();
        await AddManualCalendarBlockAsync(
            "Private appointment",
            new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.FromHours(2))
        );

        var response = await CreateVerifiedBookingAsync(
            serviceId,
            new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.FromHours(2)),
            "+27 82 555 0202",
            phoneVerificationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    protected override void RegisterMockLoggers(IServiceCollection services)
    {
        services.RemoveAll<ITwilioVerifyClient>();
        services.AddSingleton<ITwilioVerifyClient>(_ => TwilioVerifyClient);
        base.RegisterMockLoggers(services);
    }

    private async Task<JsonObject> ReadPublicProfileAsync()
    {
        var response = await AnonymousHttpClient.GetAsync("/api/main/public-booking/sea-point-studio");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private Task StartPhoneVerificationAsync(string phone)
    {
        return AnonymousHttpClient.PostAsJsonAsync("/api/main/public-booking/sea-point-studio/phone-verifications", new { phone });
    }

    private async Task<string> VerifyPhoneAsync(string phone)
    {
        await StartPhoneVerificationAsync(phone);
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/phone-verifications/check",
            new { phone, code = "123456" }
        );
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        return body!["phoneVerificationToken"]!.GetValue<string>();
    }

    private Task<HttpResponseMessage> CreateVerifiedBookingAsync(string serviceId, DateTimeOffset startAt, string phone, string phoneVerificationToken)
    {
        return AnonymousHttpClient.PostAsJsonAsync(
            "/api/main/public-booking/sea-point-studio/appointments",
            new
            {
                serviceId,
                startAt,
                name = "New Public Client",
                phone,
                email = "new-public@example.com",
                phoneVerificationToken,
                answers = new Dictionary<string, string>()
            }
        );
    }

    private async Task UpdatePublicServiceVersionAsync(string serviceId)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var service = await db.BookableServices.IgnoreQueryFilters().AsTracking().SingleAsync(service => service.Id == serviceId);
        service.Name = "Express session v2";
        service.Description = "Updated short session";
        service.PriceCents = 33000;
        service.Location = "Updated studio";
        var versionNumbers = await db.BookableServiceVersions.IgnoreQueryFilters()
            .Where(version => version.ServiceId == serviceId)
            .Select(version => version.VersionNumber)
            .ToListAsync();
        db.BookableServiceVersions.Add(new BookableServiceVersion
        {
            TenantId = service.TenantId,
            ServiceId = service.Id,
            VersionNumber = versionNumbers.Count == 0 ? 1 : versionNumbers.Max() + 1,
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
        });
        await db.SaveChangesAsync();
    }

    private async Task AddManualCalendarBlockAsync(string title, DateTimeOffset startAt, DateTimeOffset endAt)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var profile = await db.BusinessProfiles.IgnoreQueryFilters().SingleAsync(profile => profile.Slug == "sea-point-studio");
        db.ManualCalendarBlocks.Add(new ManualCalendarBlock
        {
            TenantId = profile.TenantId,
            Title = title,
            StartAt = startAt,
            EndAt = endAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private sealed class FakeTwilioVerifyClient : ITwilioVerifyClient
    {
        public string? LastStartedPhone { get; private set; }
        public bool ShouldThrowAuthenticationError { get; init; }

        public Task<TwilioVerificationStarted> StartVerificationAsync(string phone, CancellationToken cancellationToken)
        {
            if (ShouldThrowAuthenticationError)
            {
                throw new InvalidOperationException("{\"code\":20003,\"message\":\"Authenticate\",\"status\":401}");
            }
            LastStartedPhone = phone;
            return Task.FromResult(new TwilioVerificationStarted("VE_test", "pending"));
        }

        public Task<bool> CheckVerificationAsync(string phone, string code, CancellationToken cancellationToken)
        {
            if (ShouldThrowAuthenticationError)
            {
                throw new InvalidOperationException("{\"code\":20003,\"message\":\"Authenticate\",\"status\":401}");
            }
            return Task.FromResult(code == "123456");
        }
    }
}
