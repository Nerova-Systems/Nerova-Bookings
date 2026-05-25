using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Apps.Connectors.Zoom;
using Main.Features.Apps.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Main.Tests.Apps.Connectors.Zoom;

public sealed class ZoomServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateMeetingAsync_WhenCalled_ShouldPostExpectedBodyAndReturnLink()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = JsonSerializer.Serialize(new
            {
                id = 81234567890L,
                join_url = "https://zoom.us/j/81234567890?pwd=abc",
                password = "abc"
            });
            return Response(HttpStatusCode.Created, json);
        });
        var service = BuildService(handler, NewBlob());

        var input = new BookingEvent(
            Title: "Discovery Call",
            Description: "Intro chat",
            StartTime: new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            EndTime: new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            TimeZone: "Europe/Copenhagen",
            OrganizerEmail: "host@example.com",
            OrganizerName: "Host",
            Attendees: [new BookingEventAttendee("guest@example.com", "Guest")]
        );

        var link = await service.CreateMeetingAsync(input, CancellationToken.None);

        link.ExternalId.Should().Be("81234567890");
        link.JoinUrl.Should().Be("https://zoom.us/j/81234567890?pwd=abc");
        link.Password.Should().Be("abc");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().EndWith("/users/me/meetings");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("access-1");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("topic").GetString().Should().Be("Discovery Call");
        doc.RootElement.GetProperty("type").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("duration").GetInt32().Should().Be(30);
        doc.RootElement.GetProperty("timezone").GetString().Should().Be("Europe/Copenhagen");
        doc.RootElement.GetProperty("start_time").GetString().Should().Be("2026-01-05T15:00:00Z");
        doc.RootElement.GetProperty("agenda").GetString().Should().Be("Intro chat");
    }

    [Fact]
    public async Task CancelMeetingAsync_WhenNotFound_ShouldTreatAsNoOp()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.NotFound, "gone"));
        var service = BuildService(handler, NewBlob());

        var act = async () => await service.CancelMeetingAsync("81234567890", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendWithRefresh_When401_ShouldRefreshTokenWithBasicAuthAndRetry()
    {
        var seenBearers = new List<string?>();
        var createCount = 0;
        var tokenRequests = new List<(string? AuthScheme, string Body)>();
        var handler = new RecordingHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith("/users/me/meetings"))
            {
                createCount++;
                seenBearers.Add(request.Headers.Authorization?.Parameter);
                if (createCount == 1) return Response(HttpStatusCode.Unauthorized, "expired");
                var json = JsonSerializer.Serialize(new
                {
                    id = 999L,
                    join_url = "https://zoom.us/j/999",
                    password = (string?)null
                });
                return Response(HttpStatusCode.Created, json);
            }

            if (url.EndsWith("/oauth/token"))
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                tokenRequests.Add((request.Headers.Authorization?.Scheme, body));
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "access-2",
                    refresh_token = "refresh-2",
                    expires_in = 3600,
                    scope = "meeting:write",
                    token_type = "bearer"
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

        var input = new BookingEvent(
            "T", null,
            new DateTimeOffset(2026, 1, 5, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 5, 15, 30, 0, TimeSpan.Zero),
            "UTC", "h@x", null, []
        );
        var link = await service.CreateMeetingAsync(input, CancellationToken.None);

        link.ExternalId.Should().Be("999");
        createCount.Should().Be(2);
        seenBearers[0].Should().Be("access-1");
        seenBearers[1].Should().Be("access-2");

        tokenRequests.Should().HaveCount(1);
        tokenRequests[0].AuthScheme.Should().Be("Basic");
        tokenRequests[0].Body.Should().Contain("grant_type=refresh_token");
        tokenRequests[0].Body.Should().Contain("refresh_token=refresh-1");

        persisted.Should().HaveCount(1);
        persisted[0].Should().Contain("access-2");
        // Zoom rotated the refresh token — the persisted blob carries the new one.
        persisted[0].Should().Contain("refresh-2");
        service.CurrentBlob.AccessToken.Should().Be("access-2");
        service.CurrentBlob.RefreshToken.Should().Be("refresh-2");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ZoomCredentialBlob NewBlob()
    {
        return new ZoomCredentialBlob("access-1", "refresh-1", Now.AddHours(1), "meeting:write");
    }

    private static ZoomService BuildService(
        HttpMessageHandler handler,
        ZoomCredentialBlob blob,
        Func<string, CancellationToken, Task>? persist = null
    )
    {
        var options = new ZoomOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ApiBaseUrl = "https://zoom.test/v2",
            TokenUrl = "https://zoom.test/oauth/token"
        };
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);
        return new ZoomService(
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
