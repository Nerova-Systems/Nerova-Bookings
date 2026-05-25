using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Apps.Connectors.GoogleCalendar;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Main.Tests.Apps.Connectors.GoogleCalendar;

public sealed class GoogleCalendarServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetBusyTimesAsync_WhenGoogleReturnsBusyArray_ShouldParseIntervals()
    {
        const string body = """
            {
              "calendars": {
                "primary": {
                  "busy": [
                    { "start": "2026-01-02T09:00:00Z", "end": "2026-01-02T10:00:00Z" },
                    { "start": "2026-01-02T13:00:00Z", "end": "2026-01-02T14:30:00Z" }
                  ]
                }
              }
            }
            """;

        var handler = new RecordingHandler(_ => Response(HttpStatusCode.OK, body));
        var service = BuildService(handler, NewBlob());

        var busy = await service.GetBusyTimesAsync(Now, Now.AddDays(1), CancellationToken.None);

        busy.Should().HaveCount(2);
        busy[0].Start.Should().Be(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));
        busy[0].End.Should().Be(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));
        busy[1].End.Should().Be(new DateTimeOffset(2026, 1, 2, 14, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateEventAsync_WhenCalled_ShouldPostExpectedBodyAndReturnId()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response(HttpStatusCode.OK, """{"id":"abc123"}""");
        });
        var service = BuildService(handler, NewBlob());

        var input = new BookingEvent(
            Title: "Discovery Call",
            Description: "Intro chat",
            StartTime: new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            EndTime: new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            TimeZone: "Europe/Copenhagen",
            OrganizerEmail: "host@example.com",
            OrganizerName: "Host Person",
            Attendees: [new BookingEventAttendee("guest@example.com", "Guest")],
            Location: "https://meet.example/abc",
            ICalUid: "uid-1"
        );

        var id = await service.CreateEventAsync(input, CancellationToken.None);

        id.Should().Be("abc123");
        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().EndWith("/calendars/primary/events");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("access-1");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("summary").GetString().Should().Be("Discovery Call");
        doc.RootElement.GetProperty("iCalUID").GetString().Should().Be("uid-1");
        doc.RootElement.GetProperty("start").GetProperty("timeZone").GetString().Should().Be("Europe/Copenhagen");
        doc.RootElement.GetProperty("attendees")[0].GetProperty("email").GetString().Should().Be("guest@example.com");
    }

    [Fact]
    public async Task CancelEventAsync_When410Gone_ShouldSucceed()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.Gone, "gone"));
        var service = BuildService(handler, NewBlob());

        var act = async () => await service.CancelEventAsync("evt-1", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithRefresh_When401_ShouldRefreshTokenAndRetryWithNewBearer()
    {
        var seenBearers = new List<string?>();
        var freeBusyCount = 0;
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.EndsWith("/freeBusy"))
            {
                freeBusyCount++;
                seenBearers.Add(request.Headers.Authorization?.Parameter);
                if (freeBusyCount == 1) return Response(HttpStatusCode.Unauthorized, "expired");
                return Response(HttpStatusCode.OK, """{"calendars":{"primary":{"busy":[]}}}""");
            }

            if (request.RequestUri!.AbsoluteUri.EndsWith("/token"))
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "access-2",
                    expires_in = 3600,
                    scope = "scope-1",
                    token_type = "Bearer"
                });
                return Response(HttpStatusCode.OK, json);
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
        freeBusyCount.Should().Be(2);
        seenBearers[0].Should().Be("access-1");
        seenBearers[1].Should().Be("access-2");
        persisted.Should().HaveCount(1);
        persisted[0].Should().Contain("access-2");
        persisted[0].Should().Contain("refresh-1"); // refresh token preserved across refresh
        service.CurrentBlob.AccessToken.Should().Be("access-2");
        service.CurrentBlob.RefreshToken.Should().Be("refresh-1");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GoogleCredentialBlob NewBlob()
    {
        return new GoogleCredentialBlob("access-1", "refresh-1", Now.AddHours(1), "scope-1");
    }

    private static GoogleCalendarService BuildService(
        HttpMessageHandler handler,
        GoogleCredentialBlob blob,
        Func<string, CancellationToken, Task>? persist = null
    )
    {
        var options = new GoogleCalendarOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ApiBaseUrl = "https://calendar.test",
            TokenUrl = "https://oauth.test/token"
        };
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);
        return new GoogleCalendarService(
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
