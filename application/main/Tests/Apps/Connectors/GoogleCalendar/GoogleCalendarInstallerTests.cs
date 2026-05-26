using System.Net;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps.Connectors.GoogleCalendar;

public sealed class GoogleCalendarInstallerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginInstallAsync_WhenConfigured_ShouldBuildAuthorizeUrlWithExpectedParameters()
    {
        var installer = BuildInstaller(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await installer.BeginInstallAsync(
            new AppInstallContext(
                new TenantId(1),
                new UserId("usr_1"),
                "host@example.com",
                "https://app.test/api/apps/google-calendar/callback",
                "state-123"
            ),
            CancellationToken.None
        );

        result.State.Should().Be("state-123");
        var uri = new Uri(result.AuthorizeUrl);
        var query = HttpUtility.ParseQueryString(uri.Query);
        query["client_id"].Should().Be("client-abc");
        query["redirect_uri"].Should().Be("https://app.test/api/apps/google-calendar/callback");
        query["response_type"].Should().Be("code");
        query["access_type"].Should().Be("offline");
        query["prompt"].Should().Be("consent");
        query["state"].Should().Be("state-123");
        query["login_hint"].Should().Be("host@example.com");
        query["scope"].Should().Contain("calendar.events");
    }

    [Fact]
    public async Task BeginInstallAsync_WhenNotConfigured_ShouldThrowNotConfigured()
    {
        var installer = BuildInstaller(
            new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            "",
            ""
        );

        var act = async () => await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("u"), "e@x", "https://r", "s"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<GoogleCalendarNotConfiguredException>();
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenTokenExchangeSucceeds_ShouldReturnEncryptedBlob()
    {
        string? capturedFormBody = null;
        HttpMethod? capturedMethod = null;
        var handler = new RecordingHandler(request =>
            {
                capturedMethod = request.Method;
                capturedFormBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var json = JsonSerializer.Serialize(new
                    {
                        access_token = "atk",
                        refresh_token = "rtk",
                        expires_in = 3600,
                        scope = "https://www.googleapis.com/auth/calendar.events",
                        token_type = "Bearer"
                    }
                );
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }
        );

        var (installer, protector) = BuildInstallerWithProtector(handler);

        var result = await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(
                new TenantId(1),
                new UserId("usr_1"),
                "the-code",
                "https://app.test/api/apps/google-calendar/callback"
            ),
            CancellationToken.None
        );

        capturedMethod.Should().Be(HttpMethod.Post);
        capturedFormBody.Should().NotBeNull();
        capturedFormBody!.Should().Contain("code=the-code");
        capturedFormBody.Should().Contain("grant_type=authorization_code");
        capturedFormBody.Should().Contain("client_id=client-abc");
        capturedFormBody.Should().Contain("client_secret=secret-xyz");

        result.EncryptedKey.Should().NotBeNullOrEmpty();
        result.EncryptedKey.Should().NotContain("atk"); // encrypted blob does not contain the plaintext token

        var decrypted = GoogleCredentialBlob.FromJson(protector.Unprotect(result.EncryptedKey));
        decrypted.AccessToken.Should().Be("atk");
        decrypted.RefreshToken.Should().Be("rtk");
        decrypted.ExpiresAt.Should().Be(Now.AddSeconds(3600 - 60));
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenRefreshTokenMissing_ShouldThrow()
    {
        var handler = new RecordingHandler(_ =>
            {
                var json = JsonSerializer.Serialize(new { access_token = "atk", expires_in = 3600 });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }
        );
        var (installer, _) = BuildInstallerWithProtector(handler);

        var act = async () => await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(new TenantId(1), new UserId("u"), "code", "https://r"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refresh_token*");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GoogleCalendarInstaller BuildInstaller(
        HttpMessageHandler handler,
        string clientId = "client-abc",
        string clientSecret = "secret-xyz"
    )
    {
        return BuildInstallerWithProtector(handler, clientId, clientSecret).Installer;
    }

    private static (GoogleCalendarInstaller Installer, CredentialProtector Protector) BuildInstallerWithProtector(
        HttpMessageHandler handler,
        string clientId = "client-abc",
        string clientSecret = "secret-xyz"
    )
    {
        var options = Substitute.For<IOptionsMonitor<GoogleCalendarOptions>>();
        options.CurrentValue.Returns(new GoogleCalendarOptions
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                AuthorizeUrl = "https://oauth.test/authorize",
                TokenUrl = "https://oauth.test/token",
                RevokeUrl = "https://oauth.test/revoke",
                ApiBaseUrl = "https://calendar.test"
            }
        );

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(GoogleCalendarSlug.HttpClientName).Returns(_ => new HttpClient(handler));

        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        var installer = new GoogleCalendarInstaller(
            options,
            factory,
            protector,
            timeProvider,
            NullLogger<GoogleCalendarInstaller>.Instance
        );
        return (installer, protector);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
