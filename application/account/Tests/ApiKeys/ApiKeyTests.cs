using Account.Features.ApiKeys.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.ApiKeys;

/// <summary>
///     Unit tests for <see cref="ApiKey" /> aggregate invariants.
///     Pure in-memory — no database required.
/// </summary>
public sealed class ApiKeyTests
{
    private static readonly TenantId SomeTenantId = TenantId.NewId();
    private static readonly UserId SomeUserId = UserId.NewId();

    // ──────────────────────────────────────────────────────────────────────────
    // CreateUserKey
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateUserKey_ShouldSetScopeUserAndTenantId()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "My Key", null);

        key.Scope.Should().Be(ApiKeyScope.User);
        key.TenantId.Should().Be(SomeTenantId);
        key.CreatedByUserId.Should().Be(SomeUserId);
        key.Name.Should().Be("My Key");
        key.ExpiresAt.Should().BeNull();
        key.RevokedAt.Should().BeNull();
        key.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void CreateUserKey_PlainText_ShouldStartWithNerovaPrefixAndHave12CharPrefix()
    {
        var (key, plainText) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);

        plainText.Should().StartWith("nerova_user_");
        key.KeyPrefix.Should().Be(plainText[..12]);
    }

    [Fact]
    public void CreateUserKey_IdShouldStartWithKeyPrefix()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);

        key.Id.ToString().Should().StartWith("key_");
    }

    [Fact]
    public void CreateUserKey_TwoKeys_ShouldHaveDifferentIdsHashesAndPrefixes()
    {
        var (a, ptA) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "A", null);
        var (b, ptB) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "B", null);

        a.Id.Should().NotBe(b.Id);
        a.KeyHash.Should().NotBe(b.KeyHash);
        ptA.Should().NotBe(ptB);
    }

    [Fact]
    public void CreateUserKey_KeyHash_ShouldBeLowerHex64Chars()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);

        key.KeyHash.Should().HaveLength(64);
        key.KeyHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void CreateUserKey_WithExpiry_ShouldPersistExpiresAt()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(30);

        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "expiring", expiry);

        key.ExpiresAt.Should().Be(expiry);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CreateOrgKey
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateOrgKey_ShouldSetScopeOrganizationAndOrgTenantId()
    {
        var orgId = TenantId.NewId();

        var (key, _) = ApiKey.CreateOrgKey(orgId, SomeUserId, "Org Key", null);

        key.Scope.Should().Be(ApiKeyScope.Organization);
        key.TenantId.Should().Be(orgId);
        key.CreatedByUserId.Should().Be(SomeUserId);
    }

    [Fact]
    public void CreateOrgKey_PlainText_ShouldStartWithNerovOrgPrefix()
    {
        var (_, plainText) = ApiKey.CreateOrgKey(TenantId.NewId(), SomeUserId, "k", null);

        plainText.Should().StartWith("nerova_org_");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IsValid
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_WhenNoRevokedAndNoExpiry_ShouldReturnTrue()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);

        key.IsValid(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRevoked_ShouldReturnFalse()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);
        key.Revoke(DateTimeOffset.UtcNow);

        key.IsValid(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpiryInFuture_ShouldReturnTrue()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(1);
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", expiry);

        key.IsValid(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenExpiryInPast_ShouldReturnFalse()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(-1);
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", expiry);

        key.IsValid(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenBothRevokedAndExpired_ShouldReturnFalse()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(-1);
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", expiry);
        key.Revoke(DateTimeOffset.UtcNow);

        key.IsValid(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MarkUsed
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MarkUsed_ShouldSetLastUsedAt()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);
        var now = DateTimeOffset.UtcNow;

        key.MarkUsed(now);

        key.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public void MarkUsed_CalledTwice_ShouldUpdateToNewerTimestamp()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);
        var first = DateTimeOffset.UtcNow;
        var second = first.AddMinutes(5);

        key.MarkUsed(first);
        key.MarkUsed(second);

        key.LastUsedAt.Should().Be(second);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Revoke
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Revoke_ShouldSetRevokedAt()
    {
        var (key, _) = ApiKey.CreateUserKey(SomeTenantId, SomeUserId, "k", null);
        var now = DateTimeOffset.UtcNow;

        key.Revoke(now);

        key.RevokedAt.Should().Be(now);
    }
}
