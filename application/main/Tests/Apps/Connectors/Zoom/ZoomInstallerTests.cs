using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Apps.Connectors.Zoom;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps.Connectors.Zoom;

public sealed class ZoomInstallerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginInstallAsync_WhenConfigured_ShouldBuildAuthorizeUrlWithExpectedParameters()
    {
        var installer = BuildInstaller(handler: new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await installer.BeginInstallAsync(
            new AppInstallContext(
                new TenantId(1),
                new UserId("usr_1"),
                "host@example.com",
                "https://app.test/api/apps/zoom/callback",
                "state-123"
            ),
            CancellationToken.None
        );

        result.State.Should().Be("state-123");
        var uri = new Uri(result.AuthorizeUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["client_id"].Should().Be("client-abc");
        query["redirect_uri"].Should().Be("https://app.test/api/apps/zoom/callback");
        query["response_type"].Should().Be("code");
        query["state"].Should().Be("state-123");
    }

    [Fact]
    public async Task BeginInstallAsync_WhenNotConfigured_ShouldThrow()
    {
        var installer = BuildInstaller(
            handler: new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            clientId: "",
            clientSecret: ""
        );

        var act = async () => await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("u"), "e@x", "https://r", "s"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<ZoomNotConfiguredException>();
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenTokenExchangeSucceeds_ShouldUseBasicAuthAndReturnEncryptedBlob()
    {
        string? capturedFormBody = null;
        AuthenticationHeaderValue? capturedAuth = null;
        HttpMethod? capturedMethod = null;
        var handler = new RecordingHandler(request =>
        {
            capturedMethod = request.Method;
            capturedAuth = request.Headers.Authorization;
            capturedFormBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = JsonSerializer.Serialize(new
            {
                access_token = "atk",
                refresh_token = "rtk",
                expires_in = 3600,
                scope = "meeting:write",
                token_type = "bearer"
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var (installer, protector) = BuildInstallerWithProtector(handler);

        var result = await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(
                new TenantId(1),
                new UserId("usr_1"),
                "the-code",
                "https://app.test/api/apps/zoom/callback"
            ),
            CancellationToken.None
        );

        capturedMethod.Should().Be(HttpMethod.Post);
        capturedAuth.Should().NotBeNull();
        capturedAuth!.Scheme.Should().Be("Basic");
        var expectedBasic = Convert.ToBase64String(Encoding.UTF8.GetBytes("client-abc:secret-xyz"));
        capturedAuth.Parameter.Should().Be(expectedBasic);

        capturedFormBody.Should().NotBeNull();
        capturedFormBody!.Should().Contain("grant_type=authorization_code");
        capturedFormBody.Should().Contain("code=the-code");
        // Zoom places creds in the Basic header — they must NOT also appear in the form body.
        capturedFormBody.Should().NotContain("client_id=");
        capturedFormBody.Should().NotContain("client_secret=");

        result.EncryptedKey.Should().NotBeNullOrEmpty();
        result.EncryptedKey.Should().NotContain("atk");

        var decrypted = ZoomCredentialBlob.FromJson(protector.Unprotect(result.EncryptedKey));
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
        });
        var (installer, _) = BuildInstallerWithProtector(handler);

        var act = async () => await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(new TenantId(1), new UserId("u"), "code", "https://r"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refresh_token*");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ZoomInstaller BuildInstaller(
        HttpMessageHandler handler,
        string clientId = "client-abc",
        string clientSecret = "secret-xyz"
    )
    {
        return BuildInstallerWithProtector(handler, clientId, clientSecret).Installer;
    }

    private static (ZoomInstaller Installer, CredentialProtector Protector) BuildInstallerWithProtector(
        HttpMessageHandler handler,
        string clientId = "client-abc",
        string clientSecret = "secret-xyz"
    )
    {
        var options = Substitute.For<IOptionsMonitor<ZoomOptions>>();
        options.CurrentValue.Returns(new ZoomOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            AuthorizeUrl = "https://zoom.test/oauth/authorize",
            TokenUrl = "https://zoom.test/oauth/token",
            RevokeUrl = "https://zoom.test/oauth/revoke",
            ApiBaseUrl = "https://zoom.test/v2"
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ZoomSlug.HttpClientName).Returns(_ => new HttpClient(handler));

        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        var installer = new ZoomInstaller(
            options,
            factory,
            protector,
            timeProvider,
            NullLogger<ZoomInstaller>.Instance
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
