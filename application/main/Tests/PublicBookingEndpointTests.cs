using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Main.Tests;

public sealed class PublicBookingEndpointTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task PublicProfile_ShouldReturnBusinessBranding()
    {
        var profile = await ReadPublicProfileAsync();

        profile["name"]!.GetValue<string>().Should().Be("Sea Point studio");
        profile["logoUrl"]!.GetValue<string>().Should().Be("/logos/sea-point-studio.svg");
    }

    [Fact]
    public async Task ClientPrefill_ShouldReturnSafeFieldsForExistingNormalizedPhone()
    {
        await ReadPublicProfileAsync();

        var response = await AnonymousHttpClient.GetAsync("/api/main/public-booking/sea-point-studio/client-prefill?phone=%2B27823417890");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body.Should().NotBeNull();
        body!["name"]!.GetValue<string>().Should().Be("Liam Botha");
        body["email"]!.GetValue<string>().Should().Be("liam@example.com");
        body.ContainsKey("id").Should().BeFalse();
        body.ContainsKey("alert").Should().BeFalse();
        body.ContainsKey("internalNote").Should().BeFalse();
    }

    [Fact]
    public async Task ClientPrefill_ShouldReturnEmptyFieldsForUnknownPhone()
    {
        await ReadPublicProfileAsync();

        var response = await AnonymousHttpClient.GetAsync("/api/main/public-booking/sea-point-studio/client-prefill?phone=%2B27999999999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body.Should().NotBeNull();
        body!["name"]!.GetValue<string>().Should().BeEmpty();
        body["email"]!.GetValue<string>().Should().BeEmpty();
    }

    [Fact]
    public async Task PublicBooking_ShouldReuseNormalizedPhoneMatchWithoutOverwritingClientDetails()
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var clients = await db.Clients.IgnoreQueryFilters().Where(client => client.Name.Contains("Liam") || client.Name == "Changed Public Name").ToListAsync();
        clients.Should().ContainSingle();
        clients[0].Name.Should().Be("Liam Botha");
        clients[0].Email.Should().Be("liam@example.com");
    }

    private async Task<JsonObject> ReadPublicProfileAsync()
    {
        var response = await AnonymousHttpClient.GetAsync("/api/main/public-booking/sea-point-studio");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }
}
