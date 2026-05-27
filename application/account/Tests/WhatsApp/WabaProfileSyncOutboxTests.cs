using Account.Features.WhatsApp.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.WhatsApp;

public sealed class WabaProfileSyncOutboxTests
{
    private static readonly TenantId TenantId = new(7001);

    [Fact]
    public void Enqueue_StartsInPendingWithZeroAttempts()
    {
        var now = DateTimeOffset.UtcNow;
        var outbox = WabaProfileSyncOutbox.Enqueue(TenantId, "phone_1", "{}", brandLogoUrl: null, now);

        outbox.Status.Should().Be(WabaProfileSyncStatus.Pending);
        outbox.Attempts.Should().Be(0);
        outbox.NextAttemptAt.Should().Be(now);
        outbox.SyncedAt.Should().BeNull();
    }

    [Fact]
    public void MarkUploading_FromPending_TransitionsAndIncrementsAttempts()
    {
        var outbox = WabaProfileSyncOutbox.Enqueue(TenantId, "phone_1", "{}", null, DateTimeOffset.UtcNow);

        outbox.MarkUploading();

        outbox.Status.Should().Be(WabaProfileSyncStatus.Uploading);
        outbox.Attempts.Should().Be(1);
    }

    [Fact]
    public void HappyPath_PendingUploadingPostingSynced()
    {
        var outbox = WabaProfileSyncOutbox.Enqueue(TenantId, "phone_1", "{}", "/logos/x.png", DateTimeOffset.UtcNow);
        outbox.MarkUploading();
        outbox.MarkPosting("handle_abc");
        var syncedAt = DateTimeOffset.UtcNow;
        outbox.MarkSynced("hash_xyz", syncedAt);

        outbox.Status.Should().Be(WabaProfileSyncStatus.Synced);
        outbox.LogoUploadHandle.Should().Be("handle_abc");
        outbox.LastSyncedLogoHash.Should().Be("hash_xyz");
        outbox.SyncedAt.Should().Be(syncedAt);
        outbox.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void MarkSynced_FromPending_Throws()
    {
        var outbox = WabaProfileSyncOutbox.Enqueue(TenantId, "phone_1", "{}", null, DateTimeOffset.UtcNow);

        var act = () => outbox.MarkSynced("hash", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_ThenRequeue_TransitionsBackToPending()
    {
        var outbox = WabaProfileSyncOutbox.Enqueue(TenantId, "phone_1", "{}", null, DateTimeOffset.UtcNow);
        outbox.MarkUploading();
        outbox.MarkFailed("boom", DateTimeOffset.UtcNow.AddMinutes(5));
        outbox.Status.Should().Be(WabaProfileSyncStatus.Failed);

        outbox.Requeue(DateTimeOffset.UtcNow.AddMinutes(10));

        outbox.Status.Should().Be(WabaProfileSyncStatus.Pending);
    }
}
