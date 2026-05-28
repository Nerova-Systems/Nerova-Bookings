using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Apps.Connectors.GoogleCalendar;
using NSubstitute;
using Xunit;

namespace Main.Tests.Apps.Connectors.GoogleMeet;

/// <summary>
///     Covers <see cref="GoogleCalendarService.CreateEventWithMeetLinkAsync" />, the wire-level
///     primitive the Google Meet connector calls. Mirrors <c>GoogleCalendarServiceTests</c>'s
///     <c>RecordingHandler</c> pattern: we stub the HTTP handler, assert the outbound request
///     carries <c>conferenceDataVersion=1</c> and a <c>conferenceData.createRequest</c> body
///     with <c>conferenceSolutionKey.type = "hangoutsMeet"</c>, and verify the response's
///     <c>hangoutLink</c> (or fallback entry-point URI) is surfaced as the JoinUrl.
/// </summary>
public sealed class GoogleMeetServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateEventWithMeetLinkAsync_WhenCalled_ShouldRequestConferenceDataAndReturnHangoutLink()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new RecordingHandler(request =>
            {
                captured = request;
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                const string response = """
                                        {
                                          "id": "evt-abc",
                                          "hangoutLink": "https://meet.google.com/abc-defg-hij",
                                          "conferenceData": {
                                            "entryPoints": [ { "uri": "https://meet.google.com/abc-defg-hij" } ]
                                          }
                                        }
                                        """;
                return Response(HttpStatusCode.OK, response);
            }
        );
        var service = BuildService(handler);

        var input = new BookingEvent(
            "Discovery Call",
            "Intro chat",
            new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            "Europe/Copenhagen",
            "host@example.com",
            "Host Person",
            [new BookingEventAttendee("guest@example.com", "Guest")],
            null,
            "uid-1"
        );

        var (eventId, joinUrl) = await service.CreateEventWithMeetLinkAsync(input, CancellationToken.None);

        eventId.Should().Be("evt-abc");
        joinUrl.Should().Be("https://meet.google.com/abc-defg-hij");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Contain("/calendars/primary/events");
        captured.RequestUri!.Query.Should().Contain("conferenceDataVersion=1");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var createRequest = doc.RootElement.GetProperty("conferenceData").GetProperty("createRequest");
        createRequest.GetProperty("conferenceSolutionKey").GetProperty("type").GetString().Should().Be("hangoutsMeet");
        // Stable request id derived from the iCalUid means retried inserts won't generate
        // multiple conferences. Verified here so a future change doesn't silently drop the
        // dedup guarantee.
        createRequest.GetProperty("requestId").GetString().Should().Be("uid-1");
    }

    [Fact]
    public async Task CreateEventWithMeetLinkAsync_WhenHangoutLinkMissing_ShouldFallBackToEntryPointUri()
    {
        const string response = """
                                {
                                  "id": "evt-xyz",
                                  "conferenceData": {
                                    "entryPoints": [ { "uri": "https://meet.google.com/fallback-uri" } ]
                                  }
                                }
                                """;
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.OK, response));
        var service = BuildService(handler);

        var input = new BookingEvent(
            "T", null,
            new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            "UTC", "h@x", null, []
        );
        var (eventId, joinUrl) = await service.CreateEventWithMeetLinkAsync(input, CancellationToken.None);

        eventId.Should().Be("evt-xyz");
        joinUrl.Should().Be("https://meet.google.com/fallback-uri");
    }

    [Fact]
    public async Task CreateEventWithMeetLinkAsync_WhenNoConferenceLink_ShouldThrow()
    {
        const string response = """{"id":"evt-no-meet"}""";
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.OK, response));
        var service = BuildService(handler);

        var input = new BookingEvent(
            "T", null,
            new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            "UTC", "h@x", null, []
        );

        var act = async () => await service.CreateEventWithMeetLinkAsync(input, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*hangoutLink*");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GoogleCalendarService BuildService(HttpMessageHandler handler)
    {
        var options = new GoogleCalendarOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ApiBaseUrl = "https://calendar.test",
            TokenUrl = "https://oauth.test/token"
        };
        var blob = new GoogleCredentialBlob("access-1", "refresh-1", Now.AddHours(1), "scope-1");
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);
        return new GoogleCalendarService(
            new HttpClient(handler),
            options,
            blob,
            (_, _) => Task.CompletedTask,
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
