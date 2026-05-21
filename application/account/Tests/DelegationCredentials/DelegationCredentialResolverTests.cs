using Account.Features.DelegationCredentials.Domain;
using Account.Features.DelegationCredentials.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using Xunit;
using Tenant = Account.Features.Tenants.Domain.Tenant;

namespace Account.Tests.DelegationCredentials;

/// <summary>
///     Unit tests for <see cref="DelegationCredentialResolver" />.
///     Uses <see cref="NSubstitute" /> for the repository and a real ephemeral
///     <see cref="DelegationCredentialEncryption" /> for encryption round-trips.
/// </summary>
public sealed class DelegationCredentialResolverTests
{
    private readonly IDelegationCredentialRepository _repository = Substitute.For<IDelegationCredentialRepository>();
    private readonly DelegationCredentialEncryption _encryption = new(new EphemeralDataProtectionProvider());
    private readonly DelegationCredentialResolver _resolver;

    private static readonly TenantId OrgId = TenantId.NewId();

    public DelegationCredentialResolverTests()
    {
        _resolver = new DelegationCredentialResolver(_repository, _encryption);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveAsync — happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenDomainMatches_ShouldReturnDecryptedCredential()
    {
        // Arrange
        const string plainBlob = """{"type":"service_account"}""";
        var encryptedBlob = _encryption.Protect(plainBlob);
        var credential = BuildActiveCredential(WorkspacePlatform.Google, "acme.com", encryptedBlob);
        _repository.GetByOrgAndPlatformAsync(OrgId, WorkspacePlatform.Google, default)
            .Returns(credential);

        // Act
        var result = await _resolver.ResolveAsync(OrgId, "user@acme.com", WorkspacePlatform.Google);

        // Assert
        result.Should().NotBeNull();
        result!.Platform.Should().Be(WorkspacePlatform.Google);
        result.AccessTokenOrServiceAccountKey.Should().Be(plainBlob);
        result.MemberEmail.Should().Be("user@acme.com");
        result.Domain.Should().Be("acme.com");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveAsync — domain mismatch
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenDomainDoesNotMatch_ShouldReturnNull()
    {
        // Arrange — credential is for "acme.com" but email is "@other.com"
        var credential = BuildActiveCredential(WorkspacePlatform.Google, "acme.com", "enc");
        _repository.GetByOrgAndPlatformAsync(OrgId, WorkspacePlatform.Google, default)
            .Returns(credential);

        // Act
        var result = await _resolver.ResolveAsync(OrgId, "user@other.com", WorkspacePlatform.Google);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_DomainMatchShouldBeCaseInsensitive()
    {
        // Arrange
        const string plainBlob = "blob";
        var encryptedBlob = _encryption.Protect(plainBlob);
        var credential = BuildActiveCredential(WorkspacePlatform.Google, "acme.com", encryptedBlob);
        _repository.GetByOrgAndPlatformAsync(OrgId, WorkspacePlatform.Google, default)
            .Returns(credential);

        // Act — email domain in upper case
        var result = await _resolver.ResolveAsync(OrgId, "user@ACME.COM", WorkspacePlatform.Google);

        // Assert
        result.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveAsync — inactive credential
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenCredentialIsInactive_ShouldReturnNull()
    {
        // Arrange
        var credential = BuildActiveCredential(WorkspacePlatform.Google, "acme.com", "enc");
        credential.Disable();
        _repository.GetByOrgAndPlatformAsync(OrgId, WorkspacePlatform.Google, default)
            .Returns(credential);

        // Act
        var result = await _resolver.ResolveAsync(OrgId, "user@acme.com", WorkspacePlatform.Google);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveAsync — missing credential
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenNoCredentialExists_ShouldReturnNull()
    {
        // Arrange
        _repository.GetByOrgAndPlatformAsync(OrgId, WorkspacePlatform.Google, default)
            .Returns((DelegationCredential?)null);

        // Act
        var result = await _resolver.ResolveAsync(OrgId, "user@acme.com", WorkspacePlatform.Google);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveAsync — malformed email (no @ symbol)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenEmailHasNoAtSign_ShouldReturnNull()
    {
        // Arrange — even if a credential is configured, a malformed email should not resolve
        var credential = BuildActiveCredential(WorkspacePlatform.Google, "acme.com", "enc");
        _repository.GetByOrgAndPlatformAsync(OrgId, WorkspacePlatform.Google, default)
            .Returns(credential);

        // Act
        var result = await _resolver.ResolveAsync(OrgId, "no-at-sign", WorkspacePlatform.Google);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static DelegationCredential BuildActiveCredential(
        WorkspacePlatform platform,
        string domain,
        string encryptedBlob)
    {
        var orgTenant = Tenant.CreateOrganization("owner@test.com", 0);
        var credential = DelegationCredential.Create(orgTenant, platform, domain, encryptedBlob, UserId.NewId());
        return credential;
    }
}
