using Account.Features.AuditLog.Domain;
using Account.Features.AuditLog.Infrastructure;
using FluentAssertions;
using NSubstitute;
using SharedKernel.AuditLog;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.AuditLog;

/// <summary>
///     Unit tests for <see cref="AuditLogEmitter" />.
///     Verifies that the emitter maps <see cref="AuditLogEvent" /> fields to a well-formed
///     <see cref="AuditLogEntry" /> and serializes the metadata dictionary to JSON.
/// </summary>
public sealed class AuditLogEmitterTests
{
    private static readonly TenantId TenantId = TenantId.NewId();
    private static readonly UserId UserId = UserId.NewId();
    private readonly AuditLogEmitter _emitter;
    private readonly IAuditLogRepository _repository = Substitute.For<IAuditLogRepository>();

    public AuditLogEmitterTests()
    {
        _emitter = new AuditLogEmitter(_repository);
    }

    [Fact]
    public async Task EmitAsync_WithRequiredFields_ShouldCallAddAsync()
    {
        var evt = new AuditLogEvent(TenantId, UserId, "actor@example.com", "Membership", "Invited");

        await _emitter.EmitAsync(evt, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.TenantId == TenantId &&
                e.ActorUserId == UserId &&
                e.ActorEmail == "actor@example.com" &&
                e.Resource == "Membership" &&
                e.Action == "Invited" &&
                e.Metadata == null
            ),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task EmitAsync_WithNonEmptyMetadata_ShouldSerializeToJson()
    {
        var evt = new AuditLogEvent(
            TenantId,
            UserId,
            "actor@example.com",
            "Role",
            "Deleted",
            Metadata: new Dictionary<string, string> { ["roleName"] = "Admin", ["roleId"] = "role_abc" }
        );

        await _emitter.EmitAsync(evt, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Metadata != null &&
                e.Metadata.Contains("roleName") &&
                e.Metadata.Contains("Admin")
            ),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task EmitAsync_WithEmptyMetadata_ShouldStoreNullNotEmptyJson()
    {
        var evt = new AuditLogEvent(
            TenantId,
            UserId,
            "actor@example.com",
            "Booking",
            "Created",
            Metadata: new Dictionary<string, string>()
        );

        await _emitter.EmitAsync(evt, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e => e.Metadata == null),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task EmitAsync_WithNullMetadata_ShouldStoreNull()
    {
        var evt = new AuditLogEvent(TenantId, UserId, "actor@example.com", "ApiKey", "Revoked");

        await _emitter.EmitAsync(evt, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e => e.Metadata == null),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task EmitAsync_WithAllOptionalFields_ShouldMapAll()
    {
        var evt = new AuditLogEvent(
            TenantId,
            null,
            "system@nerova.io",
            "Tenant",
            "Deleted",
            "ten_123",
            IpAddress: "10.0.0.1",
            UserAgent: "PostmanRuntime/7.0"
        );

        await _emitter.EmitAsync(evt, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.ActorUserId == null &&
                e.ResourceId == "ten_123" &&
                e.IpAddress == "10.0.0.1" &&
                e.UserAgent == "PostmanRuntime/7.0"
            ),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task EmitAsync_WhenEventIsNull_ShouldThrowArgumentNullException()
    {
        var act = async () => await _emitter.EmitAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
