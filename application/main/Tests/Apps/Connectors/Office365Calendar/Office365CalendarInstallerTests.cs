using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps.Connectors.Office365Calendar;

public sealed class Office365CalendarInstallerTests
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
                "host@contoso.com",
                "https://app.test/api/apps/office365-calendar/callback",
                "state-456"
            ),
            CancellationToken.None
        );

        result.State.Should().Be("state-456");
        var uri = new Uri(result.AuthorizeUrl);
        uri.AbsoluteUri.Should().StartWith("https://login.microsoftonline.com/common/oauth2/v2.0/authorize");
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["client_id"].Should().Be("client-abc");
        query["redirect_uri"].Should().Be("https://app.test/api/apps/office365-calendar/callback");
        query["response_type"].Should().Be("code");
        query["response_mode"].Should().Be("query");
        query["state"].Should().Be("state-456");
        query["login_hint"].Should().Be("host@contoso.com");
        query["prompt"].Should().Be("consent");
        query["scope"].Should().Contain("offline_access");
        query["scope"].Should().Contain("Calendars.ReadWrite");
        query["scope"].Should().Contain("OnlineMeetings.ReadWrite");
    }

    [Fact]
    public async Task BeginInstallAsync_WhenNotConfigured_ShouldThrowNotConfigured()
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

        await act.Should().ThrowAsync<Office365CalendarNotConfiguredException>();
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenTokenExchangeSucceeds_ShouldReturnEncryptedBlob()
    {
        string? capturedFormBody = null;
        HttpMethod? capturedMethod = null;
        Uri? capturedUri = null;
        var handler = new RecordingHandler(request =>
        {
            capturedMethod = request.Method;
            capturedUri = request.RequestUri;
            capturedFormBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = JsonSerializer.Serialize(new
            {
                access_token = "atk",
                refresh_token = "rtk",
                expires_in = 3600,
                scope = "offline_access Calendars.ReadWrite",
                token_type = "Bearer"
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        var (installer, protector) = BuildInstallerWithProtector(handler);

        var result = await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(
                new TenantId(1),
                new UserId("usr_1"),
                "the-code",
                "https://app.test/api/apps/office365-calendar/callback"
            ),
            CancellationToken.None
        );

        capturedMethod.Should().Be(HttpMethod.Post);
        capturedUri!.AbsoluteUri.Should().Be("https://login.microsoftonline.com/common/oauth2/v2.0/token");
        capturedFormBody.Should().NotBeNull();
        capturedFormBody!.Should().Contain("code=the-code");
        capturedFormBody.Should().Contain("grant_type=authorization_code");
        capturedFormBody.Should().Contain("client_id=client-abc");
        capturedFormBody.Should().Contain("client_secret=secret-xyz");
        capturedFormBody.Should().Contain("scope=");

        result.EncryptedKey.Should().NotBeNullOrEmpty();
        result.EncryptedKey.Should().NotContain("atk"); // encrypted blob does not contain the plaintext token

        var decrypted = Office365CredentialBlob.FromJson(protector.Unprotect(result.EncryptedKey));
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

    [Fact]
    public async Task UninstallAsync_ShouldBeNoOpAndNotCallNetwork()
    {
        var calls = 0;
        var handler = new RecordingHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var (installer, protector) = BuildInstallerWithProtector(handler);
        var blob = new Office365CredentialBlob("atk", "rtk", Now.AddHours(1), "scope", null);
        var encrypted = protector.Protect(blob.ToJson());

        await installer.UninstallAsync(new TenantId(1), new UserId("u"), encrypted, CancellationToken.None);

        calls.Should().Be(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Office365CalendarInstaller BuildInstaller(
        HttpMessageHandler handler,
        string clientId = "client-abc",
        string clientSecret = "secret-xyz"
    )
    {
        return BuildInstallerWithProtector(handler, clientId, clientSecret).Installer;
    }

    private static (Office365CalendarInstaller Installer, CredentialProtector Protector) BuildInstallerWithProtector(
        HttpMessageHandler handler,
        string clientId = "client-abc",
        string clientSecret = "secret-xyz"
    )
    {
        var options = Substitute.For<IOptionsMonitor<Office365CalendarOptions>>();
        options.CurrentValue.Returns(new Office365CalendarOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret
            // Leave TenantId default ("common") and the Authorize/Token URLs derived from it
            // so the test pins the real Microsoft endpoint shape.
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Office365CalendarSlug.HttpClientName).Returns(_ => new HttpClient(handler));

        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        var installer = new Office365CalendarInstaller(
            options,
            factory,
            protector,
            timeProvider,
            NullLogger<Office365CalendarInstaller>.Instance
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
