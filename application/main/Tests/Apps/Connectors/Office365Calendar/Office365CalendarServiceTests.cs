using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Apps.Connectors.Office365Calendar;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Main.Tests.Apps.Connectors.Office365Calendar;

public sealed class Office365CalendarServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetBusyTimesAsync_WhenGraphReturnsScheduleItems_ShouldParseBusyIntervals()
    {
        const string body = """
            {
              "value": [
                {
                  "scheduleId": "host@contoso.com",
                  "availabilityView": "0220",
                  "scheduleItems": [
                    { "status": "busy",      "start": { "dateTime": "2026-01-02T09:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-01-02T10:00:00.0000000", "timeZone": "UTC" } },
                    { "status": "tentative", "start": { "dateTime": "2026-01-02T13:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-01-02T14:30:00.0000000", "timeZone": "UTC" } },
                    { "status": "free",      "start": { "dateTime": "2026-01-02T15:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-01-02T16:00:00.0000000", "timeZone": "UTC" } }
                  ]
                }
              ]
            }
            """;

        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.EndsWith("/me/calendar/getSchedule"))
            {
                return Response(HttpStatusCode.OK, body);
            }

            // /me lookup for the user principal name (the seed blob has none).
            return Response(HttpStatusCode.OK, """{"mail":"host@contoso.com","userPrincipalName":"host@contoso.com"}""");
        });

        var service = BuildService(handler, NewBlob());

        var busy = await service.GetBusyTimesAsync(Now, Now.AddDays(1), CancellationToken.None);

        busy.Should().HaveCount(2);
        busy[0].Start.Should().Be(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));
        busy[0].End.Should().Be(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));
        busy[1].End.Should().Be(new DateTimeOffset(2026, 1, 2, 14, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateEventAsync_WhenCalled_ShouldPostExpectedGraphBodyAndReturnId()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response(HttpStatusCode.Created, """{"id":"AAMkAGI2..."}""");
        });
        var service = BuildService(handler, NewBlob());

        var input = new BookingEvent(
            Title: "Discovery Call",
            Description: "Intro chat",
            StartTime: new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            EndTime: new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            TimeZone: "Europe/Copenhagen",
            OrganizerEmail: "host@contoso.com",
            OrganizerName: "Host Person",
            Attendees: [new BookingEventAttendee("guest@example.com", "Guest")],
            Location: "https://meet.example/abc",
            ICalUid: "uid-1"
        );

        var id = await service.CreateEventAsync(input, CancellationToken.None);

        id.Should().Be("AAMkAGI2...");
        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().EndWith("/me/calendar/events");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("access-1");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("subject").GetString().Should().Be("Discovery Call");
        doc.RootElement.GetProperty("iCalUId").GetString().Should().Be("uid-1");
        doc.RootElement.GetProperty("start").GetProperty("timeZone").GetString().Should().Be("UTC");
        doc.RootElement.GetProperty("originalStartTimeZone").GetString().Should().Be("Europe/Copenhagen");
        doc.RootElement.GetProperty("location").GetProperty("displayName").GetString().Should().Be("https://meet.example/abc");
        var attendee = doc.RootElement.GetProperty("attendees")[0];
        attendee.GetProperty("emailAddress").GetProperty("address").GetString().Should().Be("guest@example.com");
        attendee.GetProperty("type").GetString().Should().Be("required");
    }

    [Fact]
    public async Task UpdateEventAsync_ShouldUsePatchOnMeCalendarEvents()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            return Response(HttpStatusCode.OK, "{}");
        });
        var service = BuildService(handler, NewBlob());

        var input = new BookingEvent(
            "Updated",
            null,
            Now,
            Now.AddMinutes(30),
            "UTC",
            "h@x.com",
            null,
            [new BookingEventAttendee("g@x.com", null)]
        );

        await service.UpdateEventAsync("evt-1", input, CancellationToken.None);

        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsoluteUri.Should().EndWith("/me/calendar/events/evt-1");
    }

    [Fact]
    public async Task CancelEventAsync_When404_ShouldSucceed()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.NotFound, "not found"));
        var service = BuildService(handler, NewBlob());

        var act = async () => await service.CancelEventAsync("evt-1", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithRefresh_When401_ShouldRefreshTokenAndRetryWithNewBearer()
    {
        var seenBearers = new List<string?>();
        var meCount = 0;
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/me?"))
            {
                meCount++;
                seenBearers.Add(request.Headers.Authorization?.Parameter);
                if (meCount == 1) return Response(HttpStatusCode.Unauthorized, "expired");
                return Response(HttpStatusCode.OK, """{"mail":"host@contoso.com","userPrincipalName":"host@contoso.com"}""");
            }

            if (request.RequestUri!.AbsoluteUri.EndsWith("/oauth2/v2.0/token"))
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "access-2",
                    refresh_token = "refresh-2",
                    expires_in = 3600,
                    scope = "offline_access Calendars.ReadWrite",
                    token_type = "Bearer"
                });
                return Response(HttpStatusCode.OK, json);
            }

            if (request.RequestUri!.AbsoluteUri.EndsWith("/me/calendar/getSchedule"))
            {
                return Response(HttpStatusCode.OK, """{"value":[]}""");
            }

            return Response(HttpStatusCode.NotFound, "");
        });

        var persisted = new List<string>();
        var service = BuildService(handler, NewBlob(), (json, _) =>
        {
            persisted.Add(json);
            return Task.CompletedTask;
        });

        var busy = await service.GetBusyTimesAsync(Now, Now.AddDays(1), CancellationToken.None);

        busy.Should().BeEmpty();
        meCount.Should().Be(2); // first 401, second success
        seenBearers[0].Should().Be("access-1");
        seenBearers[1].Should().Be("access-2");
        // Two persists expected: one when token rotates, one when UPN is cached back.
        persisted.Should().HaveCountGreaterThanOrEqualTo(1);
        persisted.Last().Should().Contain("access-2");
        // Microsoft rotates refresh tokens on every refresh — the new one wins.
        persisted.Last().Should().Contain("refresh-2");
        service.CurrentBlob.AccessToken.Should().Be("access-2");
        service.CurrentBlob.RefreshToken.Should().Be("refresh-2");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Office365CredentialBlob NewBlob()
    {
        // Leave UserPrincipalName null on purpose so the GetBusyTimes test covers the
        // /me fallback path; the SendWithRefresh test exercises that fallback too.
        return new Office365CredentialBlob("access-1", "refresh-1", Now.AddHours(1), "scope-1", UserPrincipalName: null);
    }

    private static Office365CalendarService BuildService(
        HttpMessageHandler handler,
        Office365CredentialBlob blob,
        Func<string, CancellationToken, Task>? persist = null
    )
    {
        var options = new Office365CalendarOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ApiBaseUrl = "https://graph.test/v1.0"
        };
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);
        return new Office365CalendarService(
            new HttpClient(handler),
            options,
            blob,
            persist ?? ((_, _) => Task.CompletedTask),
            timeProvider
        );
    }

    private static HttpResponseMessage Response(HttpStatusCode code, string body)
    {
        return new HttpResponseMessage(code) { Content = new StringContent(body) };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
