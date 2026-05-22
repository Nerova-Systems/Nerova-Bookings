using Account.Features.Sso.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Xunit;

namespace Account.Tests.SsoMicrosoft;

/// <summary>
///     Unit tests for <see cref="OrgSsoConfig" /> aggregate invariants.
///     No database — pure in-memory assertions.
/// </summary>
public sealed class OrgSsoConfigTests
{
    private const string EncryptedConfig = "enc_provider_config";
    private static readonly Tenant OrgTenant = Tenant.CreateOrganization("owner@acme.com", 0);
    private static readonly Tenant SoloTenant = Tenant.Create("solo@example.com", 0);
    private static readonly string[] AllowedDomains = ["acme.com", "acme.org"];

    // ──────────────────────────────────────────────────────────────────────────
    // Create — happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithOrganizationTenant_Succeeds()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);

        config.Id.Should().NotBeNull();
        config.Id.ToString().Should().StartWith("sso_");
        config.TenantId.Should().Be(OrgTenant.Id);
        config.Provider.Should().Be(SsoProvider.Microsoft);
        config.EncryptedProviderConfig.Should().Be(EncryptedConfig);
    }

    [Fact]
    public void Create_WithOrganizationTenant_SetsIsEnabledTrue()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);

        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_TwoConfigs_HaveDifferentIds()
    {
        var a = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, "enc_a", AllowedDomains);
        var b = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, "enc_b", AllowedDomains);

        a.Id.Should().NotBe(b.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Create — guard clause
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithNonOrganizationTenant_ThrowsInvalidOperationException()
    {
        var act = () => OrgSsoConfig.Create(SoloTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*organization tenants*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ChangesEncryptedConfigAndDomains()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);
        var newDomains = new[] { "newacme.com" };

        config.Update("new_enc_config", newDomains);

        config.EncryptedProviderConfig.Should().Be("new_enc_config");
        config.GetAllowedDomains().Should().BeEquivalentTo(newDomains);
    }

    [Fact]
    public void Update_DoesNotChangeTenantIdOrProvider()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);
        var originalTenantId = config.TenantId;

        config.Update("new_enc", ["other.com"]);

        config.TenantId.Should().Be(originalTenantId);
        config.Provider.Should().Be(SsoProvider.Microsoft);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Enable / Disable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Disable_SetsIsEnabledFalse()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);
        config.IsEnabled.Should().BeTrue(); // precondition

        config.Disable();

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_IsIdempotent()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);
        config.IsEnabled.Should().BeTrue(); // precondition

        config.Enable(); // no-op

        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_IsIdempotent()
    {
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, AllowedDomains);
        config.Disable();
        config.IsEnabled.Should().BeFalse(); // precondition

        config.Disable(); // no-op

        config.IsEnabled.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetAllowedDomains
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAllowedDomains_ReturnsDeserializedArray()
    {
        var domains = new[] { "acme.com", "acme.org", "sub.acme.co.uk" };
        var config = OrgSsoConfig.Create(OrgTenant, SsoProvider.Microsoft, EncryptedConfig, domains);

        var result = config.GetAllowedDomains();

        result.Should().BeEquivalentTo(domains);
    }
}
