using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Connectors.Domain;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class CoreConnectorProviderClientTests
{
    [Fact]
    public async Task GetBusyWindowsAsync_WhenGoogleFreeBusyResponds_ShouldPostSelectedCalendarsAndMapBusyWindows()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "calendars": {
                "primary": {
                  "busy": [
                    {
                      "start": "2026-06-01T07:00:00Z",
                      "end": "2026-06-01T07:30:00Z"
                    }
                  ]
                },
                "focus": {
                  "busy": [
                    {
                      "start": "2026-06-01T10:00:00Z",
                      "end": "2026-06-01T10:30:00Z"
                    }
                  ]
                }
              }
            }
            """
        );
        var provider = new GoogleCalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("google-access-token")
        );
        var credential = ConnectorCredential.Create(
            new TenantId(1),
            "cred_google",
            new UserId("00000000-0000-0000-0000-000000000001"),
            CoreConnectorConstants.GoogleCalendar,
            "google-account",
            "owner@example.com",
            "Owner Google",
            "connected",
            "secret://connectors/google/cred_google",
            [
                new CoreConnectorCalendar("primary", "Primary", true),
                new CoreConnectorCalendar("focus", "Focus", false)
            ]
        );
        var selectedCalendars = new[]
        {
            new EventTypeSelectedCalendar { Integration = CoreConnectorConstants.GoogleCalendar, ExternalId = "primary", CredentialId = "cred_google" },
            new EventTypeSelectedCalendar { Integration = CoreConnectorConstants.GoogleCalendar, ExternalId = "focus", CredentialId = "cred_google" },
            new EventTypeSelectedCalendar { Integration = CoreConnectorConstants.Office365Calendar, ExternalId = "calendar", CredentialId = "cred_office" }
        };

        // Act
        var busyWindows = await provider.GetBusyWindowsAsync(
            [credential],
            selectedCalendars,
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
            CancellationToken.None
        );

        // Assert
        busyWindows.Should().Equal(
            new CalendarBusyWindow(DateTimeOffset.Parse("2026-06-01T07:00:00Z"), DateTimeOffset.Parse("2026-06-01T07:30:00Z")),
            new CalendarBusyWindow(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), DateTimeOffset.Parse("2026-06-01T10:30:00Z"))
        );
        httpHandler.Request!.RequestUri.Should().Be("https://www.googleapis.com/calendar/v3/freeBusy");
        httpHandler.Request.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "google-access-token"));
        using var document = JsonDocument.Parse(httpHandler.RequestBody!);
        document.RootElement.GetProperty("timeMin").GetString().Should().Be("2026-06-01T00:00:00.0000000+00:00");
        document.RootElement.GetProperty("timeMax").GetString().Should().Be("2026-06-02T00:00:00.0000000+00:00");
        document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()).Should().Equal("primary", "focus");
    }

    [Fact]
    public async Task GetBusyWindowsAsync_WhenOffice365CalendarViewResponds_ShouldBatchSelectedCalendarsAndMapBusyWindows()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "responses": [
                {
                  "id": "0",
                  "status": 200,
                  "body": {
                    "value": [
                      {
                        "showAs": "busy",
                        "start": { "dateTime": "2026-06-01T08:00:00Z", "timeZone": "UTC" },
                        "end": { "dateTime": "2026-06-01T08:30:00Z", "timeZone": "UTC" }
                      },
                      {
                        "showAs": "free",
                        "start": { "dateTime": "2026-06-01T09:00:00Z", "timeZone": "UTC" },
                        "end": { "dateTime": "2026-06-01T09:30:00Z", "timeZone": "UTC" }
                      }
                    ]
                  }
                },
                {
                  "id": "1",
                  "status": 200,
                  "body": {
                    "value": [
                      {
                        "showAs": "tentative",
                        "start": { "dateTime": "2026-06-01T11:00:00Z", "timeZone": "UTC" },
                        "end": { "dateTime": "2026-06-01T11:30:00Z", "timeZone": "UTC" }
                      }
                    ]
                  }
                }
              ]
            }
            """
        );
        var provider = new Office365CalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("office-access-token")
        );
        var credential = ConnectorCredential.Create(
            new TenantId(1),
            "cred_office",
            new UserId("00000000-0000-0000-0000-000000000001"),
            CoreConnectorConstants.Office365Calendar,
            "office-account",
            "owner@example.com",
            "Owner Office",
            "connected",
            "secret://connectors/office365/cred_office",
            [
                new CoreConnectorCalendar("calendar", "Calendar", true),
                new CoreConnectorCalendar("team", "Team", false)
            ]
        );
        var selectedCalendars = new[]
        {
            new EventTypeSelectedCalendar { Integration = CoreConnectorConstants.Office365Calendar, ExternalId = "calendar", CredentialId = "cred_office" },
            new EventTypeSelectedCalendar { Integration = CoreConnectorConstants.Office365Calendar, ExternalId = "team", CredentialId = "cred_office" },
            new EventTypeSelectedCalendar { Integration = CoreConnectorConstants.GoogleCalendar, ExternalId = "primary", CredentialId = "cred_google" }
        };

        // Act
        var busyWindows = await provider.GetBusyWindowsAsync(
            [credential],
            selectedCalendars,
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
            CancellationToken.None
        );

        // Assert
        busyWindows.Should().Equal(
            new CalendarBusyWindow(DateTimeOffset.Parse("2026-06-01T08:00:00Z"), DateTimeOffset.Parse("2026-06-01T08:30:00Z")),
            new CalendarBusyWindow(DateTimeOffset.Parse("2026-06-01T11:00:00Z"), DateTimeOffset.Parse("2026-06-01T11:30:00Z"))
        );
        httpHandler.Request!.RequestUri.Should().Be("https://graph.microsoft.com/v1.0/$batch");
        httpHandler.Request.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "office-access-token"));
        using var document = JsonDocument.Parse(httpHandler.RequestBody!);
        var requests = document.RootElement.GetProperty("requests").EnumerateArray().ToArray();
        requests.Should().HaveCount(2);
        requests.Select(request => request.GetProperty("url").GetString()).Should().Equal(
            "/me/calendars/calendar/calendarView?startDateTime=2026-06-01T00%3A00%3A00.0000000%2B00%3A00&endDateTime=2026-06-02T00%3A00%3A00.0000000%2B00%3A00&$select=showAs,start,end&$top=999",
            "/me/calendars/team/calendarView?startDateTime=2026-06-01T00%3A00%3A00.0000000%2B00%3A00&endDateTime=2026-06-02T00%3A00%3A00.0000000%2B00%3A00&$select=showAs,start,end&$top=999"
        );
    }

    private sealed class StaticCoreConnectorAccessTokenProvider(string accessToken) : ICoreConnectorAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(ConnectorCredential credential, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(accessToken);
        }
    }

    private sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return httpClient;
        }
    }

    private sealed class CapturingHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
