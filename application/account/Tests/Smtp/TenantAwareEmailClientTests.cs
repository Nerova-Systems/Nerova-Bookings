using Account.Features.Smtp.Domain;
using Account.Features.Smtp.Infrastructure;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Integrations.Email;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Smtp;

/// <summary>
///     Unit tests for <see cref="TenantAwareEmailClient" /> routing logic.
///     Verifies that the decorator falls back to the platform client under all expected conditions,
///     and routes to the org SMTP client when all prerequisites are met.
/// </summary>
public sealed class TenantAwareEmailClientTests
{
    private readonly IEmailClient _platformClient = Substitute.For<IEmailClient>();
    private readonly IOrgSmtpConfigRepository _configRepository = Substitute.For<IOrgSmtpConfigRepository>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly SmtpCredentialProtector _credentialProtector =
        new(new EphemeralDataProtectionProvider());

    private readonly TenantAwareEmailClient _sut;

    private static readonly EmailMessage SampleMessage = new(
        Recipient: "recipient@example.com",
        Subject: "Test",
        HtmlBody: "<p>Hello</p>",
        PlainTextBody: "Hello"
    );

    public TenantAwareEmailClientTests()
    {
        _sut = new TenantAwareEmailClient(
            _platformClient,
            _configRepository,
            _executionContext,
            _credentialProtector
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback — no org context
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenNoActiveOrg_ShouldUsePlatformClient()
    {
        // Arrange
        _executionContext.ActiveOrgId.Returns((TenantId?)null);
        _executionContext.UserInfo.Returns(UserInfoWithFlag());

        // Act
        await _sut.SendAsync(SampleMessage, CancellationToken.None);

        // Assert
        await _platformClient.Received(1).SendAsync(SampleMessage, CancellationToken.None);
        await _configRepository.DidNotReceive().GetByOrgIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback — feature flag off
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenCapFlagDisabled_ShouldUsePlatformClient()
    {
        // Arrange
        var orgId = TenantId.NewId();
        _executionContext.ActiveOrgId.Returns(orgId);
        _executionContext.UserInfo.Returns(UserInfoWithoutFlag());

        // Act
        await _sut.SendAsync(SampleMessage, CancellationToken.None);

        // Assert
        await _platformClient.Received(1).SendAsync(SampleMessage, CancellationToken.None);
        await _configRepository.DidNotReceive().GetByOrgIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback — no config in database
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenNoSmtpConfigExists_ShouldUsePlatformClient()
    {
        // Arrange
        var orgId = TenantId.NewId();
        _executionContext.ActiveOrgId.Returns(orgId);
        _executionContext.UserInfo.Returns(UserInfoWithFlag());
        _configRepository.GetByOrgIdAsync(orgId, Arg.Any<CancellationToken>()).Returns((OrgSmtpConfig?)null);

        // Act
        await _sut.SendAsync(SampleMessage, CancellationToken.None);

        // Assert
        await _platformClient.Received(1).SendAsync(SampleMessage, CancellationToken.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback — config exists but is disabled
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenSmtpConfigIsDisabled_ShouldUsePlatformClient()
    {
        // Arrange
        var orgId = TenantId.NewId();
        var orgTenant = Tenant.CreateOrganization("owner@acme.com", 0);
        var config = OrgSmtpConfig.Create(orgTenant, "smtp.acme.com", 587, true, "user", "enc", "from@acme.com", null, null);
        config.Disable();

        _executionContext.ActiveOrgId.Returns(orgId);
        _executionContext.UserInfo.Returns(UserInfoWithFlag());
        _configRepository.GetByOrgIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);

        // Act
        await _sut.SendAsync(SampleMessage, CancellationToken.None);

        // Assert
        await _platformClient.Received(1).SendAsync(SampleMessage, CancellationToken.None);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Org SMTP path — routes to custom SMTP, not platform
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenEnabledConfigExists_ShouldNotUsePlatformClient()
    {
        // Arrange — config pointing at a non-existent SMTP server (connection will fail but routing is correct)
        var orgId = TenantId.NewId();
        var orgTenant = Tenant.CreateOrganization("owner@acme.com", 0);
        var plainPassword = "my-secret-password";
        var encryptedPassword = _credentialProtector.Protect(plainPassword);
        var config = OrgSmtpConfig.Create(
            orgTenant,
            host: "127.0.0.1",
            port: 19876,           // no server here — connection will be refused
            useSsl: false,
            username: "user",
            encryptedPassword: encryptedPassword,
            fromEmail: "from@acme.com",
            fromName: null,
            replyToEmail: null
        );

        _executionContext.ActiveOrgId.Returns(orgId);
        _executionContext.UserInfo.Returns(UserInfoWithFlag());
        _configRepository.GetByOrgIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);

        // Act — SmtpClient.SendMailAsync throws because no server is listening; that is expected
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await _sut.SendAsync(SampleMessage, CancellationToken.None)
        );

        // Assert — platform client was NEVER called; the decorator tried org SMTP
        await _platformClient.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static UserInfo UserInfoWithFlag() => new()
    {
        IsAuthenticated = true,
        FeatureFlags = new HashSet<string> { FeatureFlagDefinitions.CapCustomSmtp.Key }
    };

    private static UserInfo UserInfoWithoutFlag() => new()
    {
        IsAuthenticated = true,
        FeatureFlags = new HashSet<string>()
    };
}
