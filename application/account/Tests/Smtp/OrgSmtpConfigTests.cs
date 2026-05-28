using Account.Features.Smtp.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Xunit;

namespace Account.Tests.Smtp;

/// <summary>
///     Unit tests for <see cref="OrgSmtpConfig" /> aggregate invariants.
///     No database — pure in-memory assertions.
/// </summary>
public sealed class OrgSmtpConfigTests
{
    private static readonly Tenant OrgTenant = Tenant.CreateOrganization("owner@acme.com", 0);
    private static readonly Tenant SoloTenant = Tenant.Create("solo@example.com", 0);

    // ──────────────────────────────────────────────────────────────────────────
    // Create — happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithOrgTenant_ShouldBuildConfigWithNewIdAndIsEnabledTrue()
    {
        var config = OrgSmtpConfig.Create(
            OrgTenant,
            "smtp.acme.com",
            587,
            true,
            "noreply@acme.com",
            "enc_secret",
            "noreply@acme.com",
            "Acme Bookings",
            "support@acme.com"
        );

        config.Id.Should().NotBeNull();
        config.Id.ToString().Should().StartWith("smtp_");
        config.TenantId.Should().Be(OrgTenant.Id);
        config.Host.Should().Be("smtp.acme.com");
        config.Port.Should().Be(587);
        config.UseSsl.Should().BeTrue();
        config.Username.Should().Be("noreply@acme.com");
        config.EncryptedPassword.Should().Be("enc_secret");
        config.FromEmail.Should().Be("noreply@acme.com");
        config.FromName.Should().Be("Acme Bookings");
        config.ReplyToEmail.Should().Be("support@acme.com");
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullOptionalFields_ShouldAllowNullFromNameAndReplyTo()
    {
        var config = OrgSmtpConfig.Create(
            OrgTenant,
            "smtp.acme.com",
            25,
            false,
            "user",
            "enc",
            "from@acme.com",
            null,
            null
        );

        config.FromName.Should().BeNull();
        config.ReplyToEmail.Should().BeNull();
    }

    [Fact]
    public void Create_TwoConfigs_ShouldHaveDifferentIds()
    {
        var a = OrgSmtpConfig.Create(OrgTenant, "smtp.a.com", 587, true, "u", "e", "from@a.com", null, null);
        var b = OrgSmtpConfig.Create(OrgTenant, "smtp.b.com", 587, true, "u", "e", "from@b.com", null, null);

        a.Id.Should().NotBe(b.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Create — guard clauses
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithNonOrgTenant_ShouldThrowInvalidOperationException()
    {
        var act = () => OrgSmtpConfig.Create(
            SoloTenant,
            "smtp.solo.com",
            587,
            true,
            "u",
            "e",
            "from@solo.com",
            null,
            null
        );

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*organization tenants*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShouldModifyAllMutableFields()
    {
        var config = OrgSmtpConfig.Create(OrgTenant, "old.smtp.com", 25, false, "olduser", "oldenc", "old@from.com", null, null);

        config.Update(
            "new.smtp.com",
            465,
            true,
            "newuser",
            "newenc",
            "new@from.com",
            "New Name",
            "new@reply.com"
        );

        config.Host.Should().Be("new.smtp.com");
        config.Port.Should().Be(465);
        config.UseSsl.Should().BeTrue();
        config.Username.Should().Be("newuser");
        config.EncryptedPassword.Should().Be("newenc");
        config.FromEmail.Should().Be("new@from.com");
        config.FromName.Should().Be("New Name");
        config.ReplyToEmail.Should().Be("new@reply.com");
    }

    [Fact]
    public void Update_ShouldNotChangeTenantIdOrIsEnabled()
    {
        var config = OrgSmtpConfig.Create(OrgTenant, "smtp.com", 587, true, "u", "e", "f@c.com", null, null);
        var originalTenantId = config.TenantId;
        var originalIsEnabled = config.IsEnabled;

        config.Update("smtp2.com", 25, false, "u2", "e2", "f2@c.com", null, null);

        config.TenantId.Should().Be(originalTenantId);
        config.IsEnabled.Should().Be(originalIsEnabled);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Enable / Disable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Disable_WhenEnabled_ShouldSetIsEnabledFalse()
    {
        var config = OrgSmtpConfig.Create(OrgTenant, "smtp.com", 587, true, "u", "e", "f@c.com", null, null);
        config.IsEnabled.Should().BeTrue(); // precondition

        config.Disable();

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_WhenDisabled_ShouldSetIsEnabledTrue()
    {
        var config = OrgSmtpConfig.Create(OrgTenant, "smtp.com", 587, true, "u", "e", "f@c.com", null, null);
        config.Disable();
        config.IsEnabled.Should().BeFalse(); // precondition

        config.Enable();

        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ShouldRemainEnabled()
    {
        var config = OrgSmtpConfig.Create(OrgTenant, "smtp.com", 587, true, "u", "e", "f@c.com", null, null);

        config.Enable(); // idempotent

        config.IsEnabled.Should().BeTrue();
    }
}
