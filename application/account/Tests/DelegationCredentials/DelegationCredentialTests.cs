using Account.Features.DelegationCredentials.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.DelegationCredentials;

/// <summary>
///     Unit tests for <see cref="DelegationCredential" /> aggregate invariants.
///     No database — pure in-memory assertions.
/// </summary>
public sealed class DelegationCredentialTests
{
    private static readonly Tenant OrgTenant = Tenant.CreateOrganization("owner@acme.com", 0);
    private static readonly Tenant SoloTenant = Tenant.Create("solo@example.com", 0);
    private static readonly UserId SomeUserId = UserId.NewId();

    // ──────────────────────────────────────────────────────────────────────────
    // Create — happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithOrgTenant_ShouldBuildCredentialWithNewId()
    {
        var credential = DelegationCredential.Create(
            OrgTenant,
            WorkspacePlatform.Google,
            domain: "acme.com",
            encryptedKeyBlob: "enc_blob",
            createdByUserId: SomeUserId);

        credential.Id.Should().NotBeNull();
        credential.Id.ToString().Should().StartWith("dcrd_");
        credential.TenantId.Should().Be(OrgTenant.Id);
        credential.Platform.Should().Be(WorkspacePlatform.Google);
        credential.Domain.Should().Be("acme.com");
        credential.EncryptedKeyBlob.Should().Be("enc_blob");
        credential.Status.Should().Be(DelegationCredentialStatus.Active);
        credential.CreatedByUserId.Should().Be(SomeUserId);
        credential.LastTestedAt.Should().BeNull();
        credential.LastTestStatus.Should().BeNull();
        credential.LastTestError.Should().BeNull();
    }

    [Fact]
    public void Create_DomainShouldBeLowerCased()
    {
        var credential = DelegationCredential.Create(
            OrgTenant,
            WorkspacePlatform.Microsoft,
            domain: "ACME.COM",
            encryptedKeyBlob: "enc_blob",
            createdByUserId: SomeUserId);

        credential.Domain.Should().Be("acme.com");
    }

    [Fact]
    public void Create_TwoCredentials_ShouldHaveDifferentIds()
    {
        var a = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "a.com", "enc_a", SomeUserId);
        var b = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Microsoft, "b.com", "enc_b", SomeUserId);

        a.Id.Should().NotBe(b.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Create — guard clauses
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithNonOrgTenant_ShouldThrowInvalidOperationException()
    {
        var act = () => DelegationCredential.Create(
            SoloTenant,
            WorkspacePlatform.Google,
            domain: "solo.com",
            encryptedKeyBlob: "enc",
            createdByUserId: SomeUserId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*organization tenants*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RotateKey
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RotateKey_ShouldUpdateBlobAndDomain()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "old.com", "old_enc", SomeUserId);

        credential.RotateKey("new_enc", "new.com");

        credential.EncryptedKeyBlob.Should().Be("new_enc");
        credential.Domain.Should().Be("new.com");
    }

    [Fact]
    public void RotateKey_DomainShouldBeLowerCased()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "old.com", "enc", SomeUserId);

        credential.RotateKey("new_enc", "UPPER.COM");

        credential.Domain.Should().Be("upper.com");
    }

    [Fact]
    public void RotateKey_ShouldNotChangeTenantIdOrPlatform()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        var originalTenantId = credential.TenantId;
        var originalPlatform = credential.Platform;

        credential.RotateKey("new_enc", "acme.com");

        credential.TenantId.Should().Be(originalTenantId);
        credential.Platform.Should().Be(originalPlatform);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Enable / Disable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Disable_WhenActive_ShouldSetStatusInactive()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        credential.Status.Should().Be(DelegationCredentialStatus.Active); // precondition

        credential.Disable();

        credential.Status.Should().Be(DelegationCredentialStatus.Inactive);
    }

    [Fact]
    public void Enable_WhenInactive_ShouldSetStatusActive()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        credential.Disable();
        credential.Status.Should().Be(DelegationCredentialStatus.Inactive); // precondition

        credential.Enable();

        credential.Status.Should().Be(DelegationCredentialStatus.Active);
    }

    [Fact]
    public void Enable_WhenAlreadyActive_ShouldRemainActive()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);

        credential.Enable(); // idempotent

        credential.Status.Should().Be(DelegationCredentialStatus.Active);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MarkTestResult
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MarkTestResult_WhenSuccess_ShouldSetSuccessStatusAndClearError()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        var testedAt = DateTimeOffset.UtcNow;

        credential.MarkTestResult(success: true, error: null, testedAt);

        credential.LastTestedAt.Should().Be(testedAt);
        credential.LastTestStatus.Should().Be(CredentialTestStatus.Success);
        credential.LastTestError.Should().BeNull();
    }

    [Fact]
    public void MarkTestResult_WhenFailure_ShouldSetFailedStatusAndError()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        var testedAt = DateTimeOffset.UtcNow;

        credential.MarkTestResult(success: false, error: "invalid_grant", testedAt);

        credential.LastTestedAt.Should().Be(testedAt);
        credential.LastTestStatus.Should().Be(CredentialTestStatus.Failed);
        credential.LastTestError.Should().Be("invalid_grant");
    }

    [Fact]
    public void MarkTestResult_Success_AfterPreviousFailure_ShouldClearError()
    {
        var credential = DelegationCredential.Create(OrgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        credential.MarkTestResult(success: false, error: "some_error", DateTimeOffset.UtcNow);

        credential.MarkTestResult(success: true, error: null, DateTimeOffset.UtcNow);

        credential.LastTestStatus.Should().Be(CredentialTestStatus.Success);
        credential.LastTestError.Should().BeNull();
    }
}
