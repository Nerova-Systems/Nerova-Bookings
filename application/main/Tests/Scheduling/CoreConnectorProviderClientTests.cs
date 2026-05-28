using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Connectors.Domain;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
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

    [Fact]
    public async Task CreateCalendarEventAsync_WhenGoogleMeetIsConfigured_ShouldCreateCalendarEventWithConferenceData()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "id": "google-event-123",
              "iCalUID": "ical-google-123",
              "hangoutLink": "https://meet.google.com/abc-defg-hij"
            }
            """
        );
        var provider = new GoogleCalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("google-access-token")
        );
        var booking = CreateBooking();
        var destinationCalendar = new EventTypeDestinationCalendar
        {
            Integration = CoreConnectorConstants.GoogleCalendar,
            ExternalId = "primary",
            CredentialId = "cred_google"
        };
        var conferencing = new EventTypeDefaultConferencing
        {
            App = CoreConnectorConstants.GoogleMeet,
            CredentialId = "cred_google"
        };

        // Act
        var reference = await provider.CreateCalendarEventAsync(CreateCredential("cred_google", CoreConnectorConstants.GoogleCalendar), booking, destinationCalendar, conferencing, CancellationToken.None);

        // Assert
        reference.Should().Be(
            new BookingCalReference(
                CoreConnectorConstants.GoogleCalendar,
                "google-event-123",
                "google-event-123",
                null,
                "https://meet.google.com/abc-defg-hij",
                "primary",
                false
            )
        );
        httpHandler.Request!.Method.Should().Be(HttpMethod.Post);
        httpHandler.Request.RequestUri.Should().Be("https://www.googleapis.com/calendar/v3/calendars/primary/events?conferenceDataVersion=1&sendUpdates=none");
        httpHandler.Request.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "google-access-token"));
        using var document = JsonDocument.Parse(httpHandler.RequestBody!);
        document.RootElement.GetProperty("summary").GetString().Should().Be("Intro call");
        document.RootElement.GetProperty("description").GetString().Should().Be("A short consultation");
        document.RootElement.GetProperty("start").GetProperty("dateTime").GetString().Should().Be("2026-06-01T07:00:00.0000000+00:00");
        document.RootElement.GetProperty("end").GetProperty("dateTime").GetString().Should().Be("2026-06-01T07:30:00.0000000+00:00");
        document.RootElement.GetProperty("attendees").EnumerateArray().Select(attendee => attendee.GetProperty("email").GetString()).Should().Equal("ada@example.com");
        document.RootElement.GetProperty("conferenceData").GetProperty("createRequest").GetProperty("requestId").GetString().Should().Be(booking.Id.Value);
    }

    [Fact]
    public async Task UpdateCalendarEventAsync_WhenGoogleReferenceExists_ShouldUpdateExistingEvent()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "id": "google-event-123",
              "iCalUID": "ical-google-123",
              "hangoutLink": "https://meet.google.com/abc-defg-hij"
            }
            """
        );
        var provider = new GoogleCalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("google-access-token")
        );
        var booking = CreateBooking();
        var destinationCalendar = new EventTypeDestinationCalendar
        {
            Integration = CoreConnectorConstants.GoogleCalendar,
            ExternalId = "primary",
            CredentialId = "cred_google"
        };
        var existingReference = new BookingCalReference(CoreConnectorConstants.GoogleCalendar, "google-event-123", null, null, null, "primary", false);

        // Act
        var reference = await provider.UpdateCalendarEventAsync(
            CreateCredential("cred_google", CoreConnectorConstants.GoogleCalendar),
            booking,
            destinationCalendar,
            existingReference,
            new EventTypeDefaultConferencing { App = CoreConnectorConstants.GoogleMeet, CredentialId = "cred_google" },
            CancellationToken.None
        );

        // Assert
        reference.Uid.Should().Be("google-event-123");
        reference.MeetingUrl.Should().Be("https://meet.google.com/abc-defg-hij");
        httpHandler.Request!.Method.Should().Be(HttpMethod.Put);
        httpHandler.Request.RequestUri.Should().Be("https://www.googleapis.com/calendar/v3/calendars/primary/events/google-event-123?conferenceDataVersion=1&sendUpdates=none");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task DeleteCalendarEventAsync_WhenGoogleEventIsAlreadyGone_ShouldTreatAsSuccess(HttpStatusCode statusCode)
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler("", statusCode);
        var provider = new GoogleCalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("google-access-token")
        );
        var existingReference = new BookingCalReference(CoreConnectorConstants.GoogleCalendar, "google-event-123", null, null, null, "primary", false);

        // Act
        var act = async () => await provider.DeleteCalendarEventAsync(
            CreateCredential("cred_google", CoreConnectorConstants.GoogleCalendar),
            CreateBooking(),
            new EventTypeDestinationCalendar { Integration = CoreConnectorConstants.GoogleCalendar, ExternalId = "primary", CredentialId = "cred_google" },
            existingReference,
            CancellationToken.None
        );

        // Assert
        await act.Should().NotThrowAsync();
        httpHandler.Request!.Method.Should().Be(HttpMethod.Delete);
        httpHandler.Request.RequestUri.Should().Be("https://www.googleapis.com/calendar/v3/calendars/primary/events/google-event-123?sendUpdates=none");
    }

    [Fact]
    public async Task CreateCalendarEventAsync_WhenOffice365VideoIsConfigured_ShouldCreateOnlineMeetingEvent()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "id": "office-event-123",
              "iCalUId": "ical-office-123",
              "onlineMeeting": {
                "joinUrl": "https://teams.microsoft.com/l/meetup-join/office-event-123"
              }
            }
            """
        );
        var provider = new Office365CalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("office-access-token")
        );
        var booking = CreateBooking();

        // Act
        var reference = await provider.CreateCalendarEventAsync(
            CreateCredential("cred_office", CoreConnectorConstants.Office365Calendar),
            booking,
            new EventTypeDestinationCalendar { Integration = CoreConnectorConstants.Office365Calendar, ExternalId = "calendar", CredentialId = "cred_office" },
            new EventTypeDefaultConferencing { App = CoreConnectorConstants.Office365Video, CredentialId = "cred_office" },
            CancellationToken.None
        );

        // Assert
        reference.Should().Be(
            new BookingCalReference(
                CoreConnectorConstants.Office365Calendar,
                "office-event-123",
                "office-event-123",
                null,
                "https://teams.microsoft.com/l/meetup-join/office-event-123",
                "calendar",
                false
            )
        );
        httpHandler.Request!.Method.Should().Be(HttpMethod.Post);
        httpHandler.Request.RequestUri.Should().Be("https://graph.microsoft.com/v1.0/me/calendars/calendar/events");
        using var document = JsonDocument.Parse(httpHandler.RequestBody!);
        document.RootElement.GetProperty("subject").GetString().Should().Be("Intro call");
        document.RootElement.GetProperty("isOnlineMeeting").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("onlineMeetingProvider").GetString().Should().Be("teamsForBusiness");
    }

    [Fact]
    public async Task UpdateCalendarEventAsync_WhenOffice365ReferenceExists_ShouldPatchExistingOnlineMeetingEvent()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "id": "office-event-123",
              "iCalUId": "ical-office-123",
              "onlineMeeting": {
                "joinUrl": "https://teams.microsoft.com/l/meetup-join/office-event-123"
              }
            }
            """
        );
        var provider = new Office365CalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("office-access-token")
        );

        // Act
        var reference = await provider.UpdateCalendarEventAsync(
            CreateCredential("cred_office", CoreConnectorConstants.Office365Calendar),
            CreateBooking(),
            new EventTypeDestinationCalendar { Integration = CoreConnectorConstants.Office365Calendar, ExternalId = "calendar", CredentialId = "cred_office" },
            new BookingCalReference(CoreConnectorConstants.Office365Calendar, "office-event-123", null, null, null, "calendar", false),
            new EventTypeDefaultConferencing { App = CoreConnectorConstants.Office365Video, CredentialId = "cred_office" },
            CancellationToken.None
        );

        // Assert
        reference.Uid.Should().Be("office-event-123");
        reference.MeetingUrl.Should().Be("https://teams.microsoft.com/l/meetup-join/office-event-123");
        httpHandler.Request!.Method.Should().Be(HttpMethod.Patch);
        httpHandler.Request.RequestUri.Should().Be("https://graph.microsoft.com/v1.0/me/calendar/events/office-event-123");
        using var document = JsonDocument.Parse(httpHandler.RequestBody!);
        document.RootElement.GetProperty("isOnlineMeeting").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCalendarEventAsync_WhenOffice365EventIsAlreadyGone_ShouldTreatAsSuccess()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler("", HttpStatusCode.NotFound);
        var provider = new Office365CalendarCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("office-access-token")
        );

        // Act
        var act = async () => await provider.DeleteCalendarEventAsync(
            CreateCredential("cred_office", CoreConnectorConstants.Office365Calendar),
            CreateBooking(),
            new EventTypeDestinationCalendar { Integration = CoreConnectorConstants.Office365Calendar, ExternalId = "calendar", CredentialId = "cred_office" },
            new BookingCalReference(CoreConnectorConstants.Office365Calendar, "office-event-123", null, null, null, "calendar", false),
            CancellationToken.None
        );

        // Assert
        await act.Should().NotThrowAsync();
        httpHandler.Request!.Method.Should().Be(HttpMethod.Delete);
        httpHandler.Request.RequestUri.Should().Be("https://graph.microsoft.com/v1.0/me/calendar/events/office-event-123");
    }

    [Fact]
    public async Task CreateMeetingAsync_WhenZoomResponds_ShouldCreateZoomReference()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            """
            {
              "id": 987654321,
              "password": "zoom-pass",
              "join_url": "https://zoom.example.test/j/987654321"
            }
            """
        );
        var provider = new ZoomCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("zoom-access-token")
        );
        var booking = CreateBooking();

        // Act
        var reference = await provider.CreateMeetingAsync(
            CreateCredential("cred_zoom", CoreConnectorConstants.ZoomVideo),
            booking,
            new EventTypeDefaultConferencing { App = CoreConnectorConstants.ZoomVideo, CredentialId = "cred_zoom" },
            CancellationToken.None
        );

        // Assert
        reference.Should().Be(
            new BookingCalReference(
                CoreConnectorConstants.ZoomVideo,
                "987654321",
                "987654321",
                "zoom-pass",
                "https://zoom.example.test/j/987654321",
                null,
                false
            )
        );
        httpHandler.Request!.Method.Should().Be(HttpMethod.Post);
        httpHandler.Request.RequestUri.Should().Be("https://api.zoom.us/v2/users/me/meetings");
        httpHandler.Request.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "zoom-access-token"));
        using var document = JsonDocument.Parse(httpHandler.RequestBody!);
        document.RootElement.GetProperty("topic").GetString().Should().Be("Intro call");
        document.RootElement.GetProperty("start_time").GetString().Should().Be("2026-06-01T07:00:00.0000000+00:00");
        document.RootElement.GetProperty("duration").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task UpdateMeetingAsync_WhenZoomResponds_ShouldPatchAndReloadMeetingReference()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler(
            "",
            """
            {
              "id": 987654321,
              "password": "zoom-pass",
              "join_url": "https://zoom.example.test/j/987654321"
            }
            """
        );
        var provider = new ZoomCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("zoom-access-token")
        );

        // Act
        var reference = await provider.UpdateMeetingAsync(
            CreateCredential("cred_zoom", CoreConnectorConstants.ZoomVideo),
            CreateBooking(),
            new BookingCalReference(CoreConnectorConstants.ZoomVideo, "987654321", "987654321", null, null, null, false),
            new EventTypeDefaultConferencing { App = CoreConnectorConstants.ZoomVideo, CredentialId = "cred_zoom" },
            CancellationToken.None
        );

        // Assert
        reference.MeetingUrl.Should().Be("https://zoom.example.test/j/987654321");
        httpHandler.Requests[0].Method.Should().Be(HttpMethod.Patch);
        httpHandler.Requests[0].RequestUri.Should().Be("https://api.zoom.us/v2/meetings/987654321");
        httpHandler.Requests[1].Method.Should().Be(HttpMethod.Get);
        httpHandler.Requests[1].RequestUri.Should().Be("https://api.zoom.us/v2/meetings/987654321");
    }

    [Fact]
    public async Task DeleteMeetingAsync_WhenZoomMeetingIsAlreadyGone_ShouldTreatAsSuccess()
    {
        // Arrange
        var httpHandler = new CapturingHttpMessageHandler("", HttpStatusCode.NotFound);
        var provider = new ZoomCoreConnectorProvider(
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            new StaticCoreConnectorAccessTokenProvider("zoom-access-token")
        );

        // Act
        var act = async () => await provider.DeleteMeetingAsync(
            CreateCredential("cred_zoom", CoreConnectorConstants.ZoomVideo),
            CreateBooking(),
            new BookingCalReference(CoreConnectorConstants.ZoomVideo, "987654321", "987654321", null, null, null, false),
            new EventTypeDefaultConferencing { App = CoreConnectorConstants.ZoomVideo, CredentialId = "cred_zoom" },
            CancellationToken.None
        );

        // Assert
        await act.Should().NotThrowAsync();
        httpHandler.Request!.Method.Should().Be(HttpMethod.Delete);
        httpHandler.Request.RequestUri.Should().Be("https://api.zoom.us/v2/meetings/987654321");
    }

    private static Booking CreateBooking()
    {
        return Booking.Create(
            new TenantId(1),
            new UserId("00000000-0000-0000-0000-000000000001"),
            new EventTypeId("evt_01HX0000000000000000000000"),
            DateTimeOffset.Parse("2026-06-01T07:00:00Z"),
            30,
            0,
            0,
            "Intro call",
            "A short consultation",
            "integration",
            CoreConnectorConstants.GoogleMeet,
            "Ada Lovelace",
            "ada@example.com",
            "Africa/Johannesburg",
            BookingStatus.Accepted,
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
    }

    private static ConnectorCredential CreateCredential(string id, string integration)
    {
        return ConnectorCredential.Create(
            new TenantId(1),
            id,
            new UserId("00000000-0000-0000-0000-000000000001"),
            integration,
            $"{integration}-account",
            "owner@example.com",
            "Owner",
            "connected",
            $"secret://connectors/{id}",
            []
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

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public CapturingHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responses.Enqueue(CreateResponse(responseBody, statusCode));
        }

        public CapturingHttpMessageHandler(params string[] responseBodies)
        {
            foreach (var responseBody in responseBodies)
            {
                _responses.Enqueue(CreateResponse(responseBody, HttpStatusCode.OK));
            }
        }

        public HttpRequestMessage? Request => Requests.LastOrDefault();

        public List<HttpRequestMessage> Requests { get; } = [];

        public string? RequestBody => RequestBodies.LastOrDefault();

        private List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Count == 0 ? CreateResponse("", HttpStatusCode.OK) : _responses.Dequeue();
        }

        private static HttpResponseMessage CreateResponse(string responseBody, HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
