using Account.Features.AuditLog.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.AuditLog;

/// <summary>
///     Unit tests for <see cref="AuditLogEntry" /> aggregate invariants.
///     No database — pure in-memory assertions.
/// </summary>
public sealed class AuditLogEntryTests
{
    private static readonly TenantId SomeTenantId = TenantId.NewId();
    private static readonly UserId SomeUserId = UserId.NewId();

    // ──────────────────────────────────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithRequiredFields_ShouldBuildEntryWithNewId()
    {
        var entry = AuditLogEntry.Create(SomeTenantId, SomeUserId, "actor@example.com", "Membership", "Invited");

        entry.Id.Should().NotBeNull();
        entry.Id.ToString().Should().StartWith("audit_");
        entry.TenantId.Should().Be(SomeTenantId);
        entry.ActorUserId.Should().Be(SomeUserId);
        entry.ActorEmail.Should().Be("actor@example.com");
        entry.Resource.Should().Be("Membership");
        entry.Action.Should().Be("Invited");
        entry.ResourceId.Should().BeNull();
        entry.Metadata.Should().BeNull();
        entry.IpAddress.Should().BeNull();
        entry.UserAgent.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllOptionalFields_ShouldPopulateAll()
    {
        var entry = AuditLogEntry.Create(
            SomeTenantId,
            SomeUserId,
            "actor@example.com",
            "Role",
            "Deleted",
            resourceId: "role_01ABC",
            metadata: """{"roleName":"Admin"}""",
            ipAddress: "192.168.1.1",
            userAgent: "Mozilla/5.0"
        );

        entry.ResourceId.Should().Be("role_01ABC");
        entry.Metadata.Should().Be("""{"roleName":"Admin"}""");
        entry.IpAddress.Should().Be("192.168.1.1");
        entry.UserAgent.Should().Be("Mozilla/5.0");
    }

    [Fact]
    public void Create_WithNullActorUserId_ShouldBeAllowedForSystemActions()
    {
        var entry = AuditLogEntry.Create(SomeTenantId, null, "system@nerova.io", "Tenant", "Deleted");

        entry.ActorUserId.Should().BeNull();
        entry.ActorEmail.Should().Be("system@nerova.io");
    }

    [Fact]
    public void Create_TwoEntries_ShouldHaveDifferentIds()
    {
        var a = AuditLogEntry.Create(SomeTenantId, null, "system@nerova.io", "Booking", "Created");
        var b = AuditLogEntry.Create(SomeTenantId, null, "system@nerova.io", "Booking", "Created");

        a.Id.Should().NotBe(b.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Guard clauses
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WhenActorEmailIsEmpty_ShouldThrow(string? email)
    {
        var act = () => AuditLogEntry.Create(SomeTenantId, SomeUserId, email!, "Membership", "Invited");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WhenResourceIsEmpty_ShouldThrow(string? resource)
    {
        var act = () => AuditLogEntry.Create(SomeTenantId, SomeUserId, "actor@example.com", resource!, "Invited");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WhenActionIsEmpty_ShouldThrow(string? action)
    {
        var act = () => AuditLogEntry.Create(SomeTenantId, SomeUserId, "actor@example.com", "Membership", action!);

        act.Should().Throw<ArgumentException>();
    }
}
