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

public sealed class NangoIntegrationEndpointTests : EndpointBaseTest<MainDbContext>
{
    public NangoIntegrationEndpointTests()
    {
        FakeNangoClient.Reset();
    }

    [Fact]
    public async Task CreateConnectSession_WhenNangoIsNotConfigured_ShouldReturnServiceUnavailable()
    {
        await SeedShellAsync();
        FakeNangoClient.ConfigurationError = true;

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/main/integrations/connect-session",
            new { appSlug = "google-calendar" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        problem!["detail"]!.GetValue<string>().Should().Contain("Nango");
    }

    [Fact]
    public async Task CreateConnectSession_ForGoogleCalendar_ShouldSendOwnerTagsToNango()
    {
        await SeedShellAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/main/integrations/connect-session",
            new { appSlug = "google-calendar" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body!["connectLink"]!.GetValue<string>().Should().Be("https://connect.nango.dev/session/test");
        body["integrationKey"]!.GetValue<string>().Should().Be("google-calendar");

        FakeNangoClient.LastSessionRequest.Should().NotBeNull();
        FakeNangoClient.LastSessionRequest!.AllowedIntegrations.Should().Equal("google-calendar");
        FakeNangoClient.LastSessionRequest.Tags.Should().ContainKey("tenant_id");
        FakeNangoClient.LastSessionRequest.Tags.Should().Contain("owner_type", "StaffMember");
        FakeNangoClient.LastSessionRequest.Tags.Should().Contain("provider", "Google");
        FakeNangoClient.LastSessionRequest.Tags.Should().Contain("capability", "Calendar");
        FakeNangoClient.LastSessionRequest.Tags.Should().Contain("app_slug", "google-calendar");
        FakeNangoClient.LastSessionRequest.Tags["owner_id"].Should().NotBeNullOrWhiteSpace();
        FakeNangoClient.LastSessionRequest.Tags["end_user_id"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SyncConnections_ForGoogleCalendar_ShouldUpsertStaffOwnedIntegrationConnection()
    {
        await SeedShellAsync();
        FakeNangoClient.Connections.Add(new NangoConnection("google-calendar-staff", "google-calendar-staff", DateTimeOffset.Parse("2026-05-01T08:00:00Z")));

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/main/integrations/sync-connections",
            new { appSlug = "google-calendar" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var staff = await db.StaffMembers.IgnoreQueryFilters().SingleAsync();
        var connection = await db.IntegrationConnections.IgnoreQueryFilters()
            .SingleAsync(connection => connection.Provider == "Google" && connection.Capability == "Calendar" && connection.OwnerType == ConnectorOwnerType.StaffMember);
        connection.OwnerId.Should().Be(staff.Id);
        connection.ExternalConnectionId.Should().Be("google-calendar-staff");
        connection.Status.Should().Be("Connected");
        connection.LastSyncedAt.Should().Be(DateTimeOffset.Parse("2026-05-01T08:00:00Z"));
    }

    [Fact]
    public async Task CreateConnectSession_ForUnsupportedApp_ShouldReturnBadRequestWithoutCallingNango()
    {
        await SeedShellAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/main/integrations/connect-session",
            new { appSlug = "zoom-video" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FakeNangoClient.LastSessionRequest.Should().BeNull();
    }

    protected override void RegisterMockLoggers(IServiceCollection services)
    {
        base.RegisterMockLoggers(services);
        services.RemoveAll<INangoClient>();
        services.AddScoped<INangoClient, FakeNangoClient>();
    }

    private async Task SeedShellAsync()
    {
        (await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell")).EnsureSuccessStatusCode();
    }

    private sealed class FakeNangoClient : INangoClient
    {
        public static bool ConfigurationError { get; set; }
        public static NangoConnectSessionRequest? LastSessionRequest { get; private set; }
        public static List<NangoConnection> Connections { get; } = [];

        public static void Reset()
        {
            ConfigurationError = false;
            LastSessionRequest = null;
            Connections.Clear();
        }

        public Task<NangoConnectSession> CreateConnectSessionAsync(NangoConnectSessionRequest request, CancellationToken cancellationToken)
        {
            if (ConfigurationError) throw new NangoConfigurationException("Nango is not configured.");
            LastSessionRequest = request;
            return Task.FromResult(new NangoConnectSession("https://connect.nango.dev/session/test", DateTimeOffset.Parse("2026-05-01T08:30:00Z")));
        }

        public Task<IReadOnlyList<NangoConnection>> ListConnectionsAsync(string integrationKey, IReadOnlyDictionary<string, string> tags, CancellationToken cancellationToken)
        {
            if (ConfigurationError) throw new NangoConfigurationException("Nango is not configured.");
            return Task.FromResult<IReadOnlyList<NangoConnection>>(Connections);
        }

        public Task<IReadOnlyList<NangoCalendar>> ListCalendarsAsync(string integrationKey, string connectionId, CancellationToken cancellationToken)
        {
            if (ConfigurationError) throw new NangoConfigurationException("Nango is not configured.");
            return Task.FromResult<IReadOnlyList<NangoCalendar>>([new NangoCalendar("primary", "Primary calendar", true, true)]);
        }

        public Task<NangoCalendarEvent> CreateCalendarEventAsync(string integrationKey, string connectionId, NangoCalendarEventRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NangoCalendarEvent("event-id", "https://meet.google.com/test"));
        }

        public Task<NangoCalendarEvent> UpdateCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, NangoCalendarEventRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NangoCalendarEvent(eventId, "https://meet.google.com/test"));
        }

        public Task DeleteCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
