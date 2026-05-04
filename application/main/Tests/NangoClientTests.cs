using System.Net;
using System.Text;
using FluentAssertions;
using Main.Features.Appointments;
using Xunit;

namespace Main.Tests;

public sealed class NangoClientTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    [Fact]
    public async Task CreateConnectSessionAsync_ShouldSendNangoTagsAndReadConnectLink()
    {
        await EnvironmentLock.WaitAsync();
        var previousSecret = Environment.GetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY");
        var previousServerUrl = Environment.GetEnvironmentVariable("NANGO_SERVER_URL");
        try
        {
            Environment.SetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY", "test-secret");
            Environment.SetEnvironmentVariable("NANGO_SERVER_URL", "https://nango.test");
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = Json("""{"data":{"connect_link":"https://connect.nango.dev/session/test","expires_at":"2026-05-03T10:30:00Z"}}""")
            });
            var client = new NangoClient(new TestHttpClientFactory(handler));

            var result = await client.CreateConnectSessionAsync(
                new NangoConnectSessionRequest(
                    "google-calendar",
                    ["google-calendar"],
                    new Dictionary<string, string>
                    {
                        ["end_user_id"] = "staff-123",
                        ["organization_id"] = "tenant-456"
                    }
                ),
                CancellationToken.None
            );

            handler.Request!.RequestUri!.PathAndQuery.Should().Be("/connect/sessions");
            handler.Body.Should().Contain("\"allowed_integrations\":[\"google-calendar\"]");
            handler.Body.Should().Contain("\"end_user_id\":\"staff-123\"");
            handler.Body.Should().Contain("\"organization_id\":\"tenant-456\"");
            result.ConnectLink.Should().Be("https://connect.nango.dev/session/test");
            result.ExpiresAt.Should().Be(DateTimeOffset.Parse("2026-05-03T10:30:00Z"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY", previousSecret);
            Environment.SetEnvironmentVariable("NANGO_SERVER_URL", previousServerUrl);
            EnvironmentLock.Release();
        }
    }

    [Fact]
    public async Task ListConnectionsAsync_ShouldFilterByNangoTagsAndProviderConfigKey()
    {
        await EnvironmentLock.WaitAsync();
        var previousSecret = Environment.GetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY");
        var previousServerUrl = Environment.GetEnvironmentVariable("NANGO_SERVER_URL");
        try
        {
            Environment.SetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY", "test-secret");
            Environment.SetEnvironmentVariable("NANGO_SERVER_URL", "https://nango.test");
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""
                               {
                                 "connections": [
                                   {
                                     "connection_id": "slack-connection",
                                     "provider_config_key": "slack",
                                     "tags": { "end_user_id": "staff-123" },
                                     "updated_at": "2026-05-01T08:00:00Z"
                                   },
                                   {
                                     "connection_id": "google-connection",
                                     "provider_config_key": "google-calendar",
                                     "tags": { "end_user_id": "staff-123" },
                                     "updated_at": "2026-05-01T09:00:00Z"
                                   }
                                 ]
                               }
                               """)
            });
            var client = new NangoClient(new TestHttpClientFactory(handler));

            var result = await client.ListConnectionsAsync(
                "google-calendar",
                new Dictionary<string, string> { ["end_user_id"] = "staff-123" },
                CancellationToken.None
            );

            Uri.UnescapeDataString(handler.Request!.RequestUri!.Query).Should().Contain("tags[end_user_id]=staff-123");
            Uri.UnescapeDataString(handler.Request.RequestUri.Query).Should().NotContain("integrationId=");
            result.Should().ContainSingle();
            result[0].ConnectionId.Should().Be("google-connection");
            result[0].EndUserId.Should().Be("staff-123");
            result[0].LastSyncedAt.Should().Be(DateTimeOffset.Parse("2026-05-01T09:00:00Z"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY", previousSecret);
            Environment.SetEnvironmentVariable("NANGO_SERVER_URL", previousServerUrl);
            EnvironmentLock.Release();
        }
    }

    [Fact]
    public async Task ListCalendarsAsync_ShouldUseGoogleCalendarV3ProxyPath()
    {
        await EnvironmentLock.WaitAsync();
        var previousSecret = Environment.GetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY");
        var previousServerUrl = Environment.GetEnvironmentVariable("NANGO_SERVER_URL");
        try
        {
            Environment.SetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY", "test-secret");
            Environment.SetEnvironmentVariable("NANGO_SERVER_URL", "https://nango.test");
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"items":[]}""")
            });
            var client = new NangoClient(new TestHttpClientFactory(handler));

            await client.ListCalendarsAsync("google-calendar", "connection-123", CancellationToken.None);

            handler.Request!.RequestUri!.PathAndQuery.Should().Be("/proxy/calendar/v3/users/me/calendarList");
            handler.Request.Headers.GetValues("Provider-Config-Key").Should().Equal("google-calendar");
            handler.Request.Headers.GetValues("Connection-Id").Should().Equal("connection-123");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY", previousSecret);
            Environment.SetEnvironmentVariable("NANGO_SERVER_URL", previousServerUrl);
            EnvironmentLock.Release();
        }
    }

    private static StringContent Json(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}
