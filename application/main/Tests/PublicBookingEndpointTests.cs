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
        clients[0].Phone.Should().Be("+27 82 341 7890");
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

    private sealed class FakeTwilioVerifyClient : ITwilioVerifyClient
    {
        public string? LastStartedPhone { get; private set; }

        public Task<TwilioVerificationStarted> StartVerificationAsync(string phone, CancellationToken cancellationToken)
        {
            LastStartedPhone = phone;
            return Task.FromResult(new TwilioVerificationStarted("VE_test", "pending"));
        }

        public Task<bool> CheckVerificationAsync(string phone, string code, CancellationToken cancellationToken)
        {
            return Task.FromResult(code == "123456");
        }
    }
}
