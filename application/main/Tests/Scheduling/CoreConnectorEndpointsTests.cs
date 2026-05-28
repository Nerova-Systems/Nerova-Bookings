using System.Net;
using System.Net.Http.Json;
using System.Web;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.Connectors.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Authentication;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class CoreConnectorEndpointsTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _connectorsClient;
    private readonly HttpClient _noRedirectConnectorsClient;

    public CoreConnectorEndpointsTests()
    {
        var ownerWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            FeatureFlags = new HashSet<string> { "cap-delegation-credentials" }
        };
        _connectorsClient = CreateAuthenticatedHttpClient(ownerWithFlag);
        _noRedirectConnectorsClient = CreateNoRedirectAuthenticatedHttpClient(ownerWithFlag);
    }

    [Fact]
    public async Task GetCoreConnectorAccounts_WhenAccountsExist_ShouldReturnOnlyGoogleMicrosoftAndZoom()
    {
        // Arrange
        await SeedConnectorCredentialAsync("cred_google", "google-calendar", "google-account", "owner@gmail.com", "Owner Google");
        await SeedConnectorCredentialAsync("cred_office", "office365-calendar", "office-account", "owner@contoso.com", "Owner Microsoft");
        await SeedConnectorCredentialAsync("cred_zoom", "zoom-video", "zoom-account", "owner@zoom.example", "Owner Zoom");
        await SeedConnectorCredentialAsync("cred_daily", "dailyvideo", "daily-account", "owner@daily.example", "Owner Daily");

        // Act
        var response = await _connectorsClient.GetAsync("/api/connectors/core/accounts");

        // Assert
        response.EnsureSuccessStatusCode();
        var accounts = await response.DeserializeResponse<CoreConnectorAccountsResponse>();
        accounts!.Accounts.Select(account => account.Integration).Should().Equal("google-calendar", "office365-calendar", "zoom-video");
        accounts.Accounts[0].Calendars.Select(calendar => calendar.ExternalId).Should().Equal("primary", "team");
        accounts.Integrations.Select(integration => integration.Integration).Should().Equal("google-calendar", "office365-calendar", "zoom-video");
        accounts.Integrations.Should().OnlyContain(integration => integration.Configured);
        accounts.Integrations.Should().Contain(integration => integration.Integration == "google-calendar" && integration.Connected);
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
        var selectedResponse = await _connectorsClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/selected-calendars",
            new
            {
                selectedCalendars = new[]
                {
                    new { integration = "google-calendar", externalId = "primary", credentialId = "cred_google" }
                }
            }
        );
        var destinationResponse = await _connectorsClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/destination-calendar",
            new { destinationCalendar = new { integration = "google-calendar", externalId = "team", credentialId = "cred_google" } }
        );
        var conferencingResponse = await _connectorsClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/default-conferencing",
            new { defaultConferencing = new { app = "zoom-video", credentialId = "cred_zoom" } }
        );

        // Assert
        selectedResponse.ShouldBeSuccessfulGetRequest();
        destinationResponse.ShouldBeSuccessfulGetRequest();
        conferencingResponse.ShouldBeSuccessfulGetRequest();

        var eventTypeResponse = await _connectorsClient.GetAsync($"/api/event-types/{eventType.Id}");
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
        var response = await _connectorsClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );
        var repeatedResponse = await _connectorsClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );

        // Assert
        response.EnsureSuccessStatusCode();
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
        var response = await _connectorsClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Busy end time must be after busy start time.");
    }

    [Fact]
    public async Task GetAuthorizationUrl_WhenGoogleCalendarIsConfigured_ShouldReturnCalComShapedAuthorizationUrl()
    {
        // Act
        var response = await _connectorsClient.GetAsync("/api/connectors/core/google-calendar/authorization-url?returnTo=/event-types");

        // Assert
        response.EnsureSuccessStatusCode();
        var authorizationUrl = await response.DeserializeResponse<CoreConnectorAuthorizationUrlResponse>();
        authorizationUrl!.Url.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth?");
        var query = HttpUtility.ParseQueryString(new Uri(authorizationUrl.Url).Query);
        query["client_id"].Should().Be("test-google-calendar-client-id");
        query["access_type"].Should().Be("offline");
        query["prompt"].Should().Be("consent");
        query["scope"].Should().Contain("https://www.googleapis.com/auth/calendar");
        query["scope"].Should().Contain("https://www.googleapis.com/auth/userinfo.profile");
        query["redirect_uri"].Should().Be("https://localhost/api/connectors/core/google-calendar/callback");
        query["state"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompleteOAuthCallback_WhenGoogleMockCodeIsValid_ShouldCreateCredentialAndProtectedToken()
    {
        // Arrange
        var authorizationUrlResponse = await _connectorsClient.GetAsync("/api/connectors/core/google-calendar/authorization-url?returnTo=/event-types");
        authorizationUrlResponse.ShouldBeSuccessfulGetRequest();
        var authorizationUrl = await authorizationUrlResponse.DeserializeResponse<CoreConnectorAuthorizationUrlResponse>();
        var state = HttpUtility.ParseQueryString(new Uri(authorizationUrl!.Url).Query)["state"];

        // Act
        var response = await _noRedirectConnectorsClient.GetAsync($"/api/connectors/core/google-calendar/callback?code=mock-google-success&state={Uri.EscapeDataString(state!)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/event-types?connector=google-calendar");

        var accountsResponse = await _connectorsClient.GetAsync("/api/connectors/core/accounts");
        accountsResponse.ShouldBeSuccessfulGetRequest();
        var accounts = await accountsResponse.DeserializeResponse<CoreConnectorAccountsResponse>();
        var googleAccount = accounts!.Accounts.Should().ContainSingle(account => account.Integration == "google-calendar").Which;
        googleAccount.ExternalAccountId.Should().Be("google-account-1");
        googleAccount.AccountEmail.Should().Be("owner.google@example.test");
        googleAccount.Calendars.Select(calendar => calendar.ExternalId).Should().Equal("primary", "focus");

        var secretReference = Connection.ExecuteScalar<string>(
            "SELECT secret_reference FROM connector_credentials WHERE id = @id",
            [new { id = googleAccount.Id }]
        );
        secretReference.Should().StartWith("protected-connector-token:");
        Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM connector_token_secrets WHERE tenant_id = @tenant_id AND credential_id = @credential_id",
            [new { tenant_id = DatabaseSeeder.TenantId.Value, credential_id = googleAccount.Id }]
        ).Should().Be(1);
    }

    [Fact]
    public async Task CompleteOAuthCallback_WhenStateIsInvalid_ShouldRedirectWithoutCreatingCredential()
    {
        // Act
        var response = await _noRedirectConnectorsClient.GetAsync("/api/connectors/core/google-calendar/callback?code=mock-google-success&state=invalid-state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/event-types?error=invalid_state");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials", []).Should().Be(0);
    }

    [Fact]
    public async Task CompleteOAuthCallback_WhenProviderRejectsCode_ShouldRedirectWithoutCreatingCredential()
    {
        // Arrange
        var authorizationUrlResponse = await _connectorsClient.GetAsync("/api/connectors/core/zoom-video/authorization-url?returnTo=/event-types");
        authorizationUrlResponse.ShouldBeSuccessfulGetRequest();
        var authorizationUrl = await authorizationUrlResponse.DeserializeResponse<CoreConnectorAuthorizationUrlResponse>();
        var state = HttpUtility.ParseQueryString(new Uri(authorizationUrl!.Url).Query)["state"];

        // Act
        var response = await _noRedirectConnectorsClient.GetAsync($"/api/connectors/core/zoom-video/callback?code=mock-provider-error&state={Uri.EscapeDataString(state!)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/event-types?error=provider_error");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials", []).Should().Be(0);
    }

    [Fact]
    public async Task CoreConnectorAccessTokenProvider_WhenTokenIsExpired_ShouldRefreshAndPersistToken()
    {
        // Arrange
        using var serviceScope = Provider.CreateScope();
        var tokenStore = serviceScope.ServiceProvider.GetRequiredService<IConnectorTokenStore>();
        var accessTokenProvider = serviceScope.ServiceProvider.GetRequiredService<ICoreConnectorAccessTokenProvider>();
        var credential = ConnectorCredential.Create(
            DatabaseSeeder.TenantId,
            "cred_google",
            DatabaseSeeder.Tenant1Owner.Id!,
            "google-calendar",
            "google-account-1",
            "owner.google@example.test",
            "Owner Google",
            "connected",
            "protected-connector-token:secret_google",
            [new CoreConnectorCalendar("primary", "Primary calendar", true)]
        );
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<MainDbContext>();
        await dbContext.Set<ConnectorCredential>().AddAsync(credential);
        await tokenStore.SaveAsync(
            DatabaseSeeder.TenantId,
            "secret_google",
            credential.Id,
            new CoreConnectorTokenSet(
                "expired-access-token",
                "mock-google-refresh-token",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "offline",
                "Bearer"
            ),
            CancellationToken.None
        );
        await dbContext.SaveChangesAsync();

        // Act
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(credential, CancellationToken.None);

        // Assert
        accessToken.Should().Be("refreshed-google-access-token");
        var refreshedToken = await tokenStore.GetAsync(DatabaseSeeder.TenantId, "secret_google", CancellationToken.None);
        refreshedToken!.AccessToken.Should().Be("refreshed-google-access-token");
        refreshedToken.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task DeleteConnectorAccount_WhenOwned_ShouldRemoveCredentialAndToken()
    {
        // Arrange
        var authorizationUrlResponse = await _connectorsClient.GetAsync("/api/connectors/core/zoom-video/authorization-url?returnTo=/event-types");
        authorizationUrlResponse.ShouldBeSuccessfulGetRequest();
        var authorizationUrl = await authorizationUrlResponse.DeserializeResponse<CoreConnectorAuthorizationUrlResponse>();
        var state = HttpUtility.ParseQueryString(new Uri(authorizationUrl!.Url).Query)["state"];
        var callbackResponse = await _noRedirectConnectorsClient.GetAsync($"/api/connectors/core/zoom-video/callback?code=mock-zoom-success&state={Uri.EscapeDataString(state!)}");
        callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var accountId = Connection.ExecuteScalar<string>("SELECT id FROM connector_credentials WHERE integration = 'zoom-video'", []);

        // Act
        var response = await _connectorsClient.DeleteAsync($"/api/connectors/core/accounts/{accountId}");

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials WHERE id = @id", [new { id = accountId }]).Should().Be(0);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_token_secrets WHERE credential_id = @credential_id", [new { credential_id = accountId }]).Should().Be(0);
    }

    [Fact]
    public async Task DeleteConnectorAccount_WhenCredentialBelongsToAnotherUser_ShouldReturnNotFound()
    {
        // Arrange
        await SeedConnectorCredentialAsync("cred_google", "google-calendar", "google-account", "owner@gmail.com", "Owner Google");

        // Act
        var response = await AuthenticatedMemberHttpClient.DeleteAsync("/api/connectors/core/accounts/cred_google");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Cal.com apps connectors are disabled for this tenant.");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials WHERE id = 'cred_google'", []).Should().Be(1);
    }

    [Fact]
    public async Task UpdateEventTypeConnectorSettings_WhenCredentialDoesNotBelongToOwner_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await _connectorsClient.PutAsJsonAsync(
            $"/api/event-types/{eventType.Id}/connector-settings/destination-calendar",
            new { destinationCalendar = new { integration = "google-calendar", externalId = "primary", credentialId = "missing_google" } }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Connector credential 'missing_google' was not found.");
    }

    private async Task UpdateSchedulingProfileAsync(string handle)
    {
        var response = await _connectorsClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = "https://example.com/avatar.png" }
        );
        response.EnsureSuccessStatusCode();
    }

    private async Task<ScheduleResponse> CreateScheduleAsync()
    {
        var response = await _connectorsClient.PostAsJsonAsync(
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
        var response = await _connectorsClient.PostAsJsonAsync(
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
    private sealed record CoreConnectorAccountsResponse(CoreConnectorAccountResponse[] Accounts, CoreConnectorIntegrationResponse[] Integrations);

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
    private sealed record CoreConnectorIntegrationResponse(string Integration, string Label, bool Configured, bool Connected);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorAuthorizationUrlResponse(string Url);

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

public sealed class CoreConnectorProductionEndpointsTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _connectorsClient;

    public CoreConnectorProductionEndpointsTests() : base(Environments.Production)
    {
        var ownerWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            FeatureFlags = new HashSet<string> { "cap-delegation-credentials" }
        };
        _connectorsClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    [Fact]
    public async Task GetCoreConnectorAccounts_WhenProviderKeysAreMissing_ShouldReturnUnconfiguredIntegrations()
    {
        // Act
        var response = await _connectorsClient.GetAsync("/api/connectors/core/accounts");

        // Assert
        response.EnsureSuccessStatusCode();
        var accounts = await response.DeserializeResponse<CoreConnectorAccountsResponse>();
        accounts!.Accounts.Should().BeEmpty();
        accounts.Integrations.Select(integration => integration.Integration).Should().Equal("google-calendar", "office365-calendar", "zoom-video");
        accounts.Integrations.Should().OnlyContain(integration => !integration.Configured);
        accounts.Integrations.Should().OnlyContain(integration => !integration.Connected);
    }

    [Fact]
    public async Task EnsureTestCoreConnectorCredentials_WhenNotDevelopment_ShouldReturnNotFound()
    {
        // Arrange
        var busyStartTime = DateTimeOffset.Parse("2026-06-01T07:00:00Z");
        var busyEndTime = DateTimeOffset.Parse("2026-06-01T07:30:00Z");

        // Act
        var response = await _connectorsClient.PostAsJsonAsync(
            "/api/connectors/core/test-fixtures",
            new { busyStartTime, busyEndTime }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Core connector test fixtures are only available in development.");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM connector_credentials", []).Should().Be(0);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorAccountsResponse(CoreConnectorAccountResponse[] Accounts, CoreConnectorIntegrationResponse[] Integrations);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorAccountResponse(string Integration);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CoreConnectorIntegrationResponse(string Integration, string Label, bool Configured, bool Connected);
}
