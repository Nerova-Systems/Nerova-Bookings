using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class CoreConnectorEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task GetCoreConnectorAccounts_WhenAccountsExist_ShouldReturnOnlyGoogleMicrosoftAndZoom()
    {
        // Arrange
        await SeedConnectorCredentialAsync("cred_google", "google-calendar", "google-account", "owner@gmail.com", "Owner Google");
        await SeedConnectorCredentialAsync("cred_office", "office365-calendar", "office-account", "owner@contoso.com", "Owner Microsoft");
        await SeedConnectorCredentialAsync("cred_zoom", "zoom-video", "zoom-account", "owner@zoom.example", "Owner Zoom");
        await SeedConnectorCredentialAsync("cred_daily", "dailyvideo", "daily-account", "owner@daily.example", "Owner Daily");

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/connectors/core/accounts");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var accounts = await response.DeserializeResponse<CoreConnectorAccountsResponse>();
        accounts!.Accounts.Select(account => account.Integration).Should().Equal("google-calendar", "office365-calendar", "zoom-video");
        accounts.Accounts[0].Calendars.Select(calendar => calendar.ExternalId).Should().Equal("primary", "team");
    }

    [Fact]
    public async Task GetCoreConnectorAccounts_WhenFeatureFlagIsDisabled_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/connectors/core/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateEventTypeConnectorSettings_WhenValid_ShouldPersistSelectedDestinationAndDefaultConferencing()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await SeedConnectorCredentialAsync("cred_google", "google-calendar", "google-account", "owner@gmail.com", "Owner Google");
        await SeedConnectorCredentialAsync("cred_zoom", "zoom-video", "zoom-account", "owner@zoom.example", "Owner Zoom");

        // Act
        var selectedResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/selected-calendars",
            new
            {
                selectedCalendars = new[]
                {
                    new { integration = "google-calendar", externalId = "primary", credentialId = "cred_google" }
                }
            }
        );
        var destinationResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/destination-calendar",
            new { destinationCalendar = new { integration = "google-calendar", externalId = "team", credentialId = "cred_google" } }
        );
        var conferencingResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/default-conferencing",
            new { defaultConferencing = new { app = "zoom-video", credentialId = "cred_zoom" } }
        );

        // Assert
        selectedResponse.ShouldBeSuccessfulGetRequest();
        destinationResponse.ShouldBeSuccessfulGetRequest();
        conferencingResponse.ShouldBeSuccessfulGetRequest();

        var eventTypeResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}");
        eventTypeResponse.ShouldBeSuccessfulGetRequest();
        var updatedEventType = await eventTypeResponse.DeserializeResponse<EventTypeResponse>();
        updatedEventType!.Settings.SelectedCalendars.Should().ContainSingle().Which.ExternalId.Should().Be("primary");
        updatedEventType.Settings.DestinationCalendar!.ExternalId.Should().Be("team");
        updatedEventType.Settings.DefaultConferencing!.App.Should().Be("zoom-video");
    }

    [Fact]
    public async Task EnsureTestCoreConnectorCredentials_WhenDevelopment_ShouldCreateDeterministicCoreAccounts()
    {
        // Arrange
        var busyStartTime = DateTimeOffset.Parse("2026-06-01T07:00:00Z");
        var busyEndTime = DateTimeOffset.Parse("2026-06-01T07:30:00Z");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );
        var repeatedResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        repeatedResponse.ShouldBeSuccessfulGetRequest();
        var accounts = await response.DeserializeResponse<CoreConnectorAccountsResponse>();
        accounts!.Accounts.Select(account => account.Integration).Should().Equal("google-calendar", "office365-calendar", "zoom-video");
        accounts.Accounts[0].Id.Should().StartWith("fake-busy:");
        accounts.Accounts.Select(account => account.Id).Should().OnlyContain(id => id.Length <= 120);
        accounts.Accounts[0].Calendars.Select(calendar => calendar.ExternalId).Should().Equal("primary", "focus");
        accounts.Accounts[2].Calendars.Should().BeEmpty();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials", []).Should().Be(3);
    }

    [Fact]
    public async Task EnsureTestCoreConnectorCredentials_WhenBusyEndIsBeforeStart_ShouldReturnBadRequest()
    {
        // Arrange
        var busyStartTime = DateTimeOffset.Parse("2026-06-01T07:30:00Z");
        var busyEndTime = DateTimeOffset.Parse("2026-06-01T07:00:00Z");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Busy end time must be after busy start time.");
    }

    [Fact]
    public async Task UpdateEventTypeConnectorSettings_WhenCredentialDoesNotBelongToOwner_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/destination-calendar",
            new { destinationCalendar = new { integration = "google-calendar", externalId = "primary", credentialId = "missing_google" } }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Connector credential 'missing_google' was not found.");
    }

    private async Task UpdateSchedulingProfileAsync(string handle)
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = "https://example.com/avatar.png" }
        );
        response.EnsureSuccessStatusCode();
    }

    private async Task<ScheduleResponse> CreateScheduleAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/schedules",
            new
            {
                name = "Working hours",
                timeZone = "Africa/Johannesburg",
                isDefault = true,
                availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } },
                dateOverrides = Array.Empty<object>()
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleResponse>())!;
    }

    private async Task<EventTypeResponse> CreateEventTypeAsync(string scheduleId, string title, string slug)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types",
            new
            {
                title,
                slug,
                description = "A short consultation",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0,
                locationType = "link",
                locationValue = "https://example.com/meet",
                settings = (object?)null
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeResponse>())!;
    }

    private async Task SeedConnectorCredentialAsync(string id, string integration, string externalAccountId, string email, string displayName)
    {
        using var serviceScope = Provider.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<MainDbContext>();
        var calendarsJson = """
                            [{"externalId":"primary","name":"Primary calendar","primary":true},{"externalId":"team","name":"Team calendar","primary":false}]
                            """;
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO connector_credentials
                (tenant_id, id, owner_user_id, integration, external_account_id, account_email, display_name, status, secret_reference, calendars_json, created_at, modified_at)
            VALUES
                ({0}, {1}, {2}, {3}, {4}, {5}, {6}, 'connected', {7}, {8}, {9}, NULL)
            """,
            DatabaseSeeder.TenantId.Value,
            id,
            DatabaseSeeder.Tenant1Owner.Id!.Value,
            integration,
            externalAccountId,
            email,
            displayName,
            $"secret://connectors/{id}",
            calendarsJson,
            DateTimeOffset.UtcNow
        );
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorAccountsResponse(CoreConnectorAccountResponse[] Accounts);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorAccountResponse(
        string Id,
        string Integration,
        string ExternalAccountId,
        string AccountEmail,
        string DisplayName,
        string Status,
        CoreConnectorCalendarResponse[] Calendars
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorCalendarResponse(string ExternalId, string Name, bool Primary);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id, EventTypeSettingsResponse Settings);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeSettingsResponse(
        EventTypeSelectedCalendarResponse[] SelectedCalendars,
        EventTypeDestinationCalendarResponse? DestinationCalendar,
        EventTypeDefaultConferencingResponse? DefaultConferencing
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeSelectedCalendarResponse(string Integration, string ExternalId, string? CredentialId);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeDestinationCalendarResponse(string Integration, string ExternalId, string? CredentialId);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeDefaultConferencingResponse(string App, string? CredentialId);
}

public sealed class CoreConnectorProductionEndpointsTests() : EndpointBaseTest<MainDbContext>(Environments.Production)
{
    [Fact]
    public async Task EnsureTestCoreConnectorCredentials_WhenNotDevelopment_ShouldReturnNotFound()
    {
        // Arrange
        var busyStartTime = DateTimeOffset.Parse("2026-06-01T07:00:00Z");
        var busyEndTime = DateTimeOffset.Parse("2026-06-01T07:30:00Z");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Core connector test fixtures are only available in development.");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials", []).Should().Be(0);
    }
}
