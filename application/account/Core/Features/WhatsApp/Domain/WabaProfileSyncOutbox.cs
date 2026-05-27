using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.WhatsApp.Domain;

[PublicAPI]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<long, WabaProfileSyncOutboxId>))]
public sealed record WabaProfileSyncOutboxId(long Value) : StronglyTypedLongId<WabaProfileSyncOutboxId>(Value)
{
    public override string ToString()
    {
        return Value.ToString();
    }
}

/// <summary>
///     Lifecycle of a single outbox row. The Phase 7b sync job is the only writer that advances
///     past <see cref="Pending" />; the command handler only ever creates rows in
///     <see cref="Pending" />. Transition matrix (enforced by <see cref="WabaProfileSyncOutbox" />):
///     <list type="bullet">
///         <item>Pending → Uploading | Posting | Failed</item>
///         <item>Uploading → Posting | Failed</item>
///         <item>Posting → Synced | Failed</item>
///         <item>Failed → Pending (retry scheduled by job)</item>
///         <item>Synced is terminal.</item>
///     </list>
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WabaProfileSyncStatus
{
    Pending,
    Uploading,
    Posting,
    Synced,
    Failed
}

/// <summary>
///     One row per pending profile sync to Meta. The command handler enqueues a row in
///     <see cref="WabaProfileSyncStatus.Pending" /> after persisting the tenant's
///     <c>BrandProfile</c>; the Phase 7b TickerQ job picks rows by
///     <c>(Status, NextAttemptAt)</c>, uploads the logo if required, posts the
///     <c>whatsapp_business_profile</c> body, and advances the row to
///     <see cref="WabaProfileSyncStatus.Synced" /> or <see cref="WabaProfileSyncStatus.Failed" />.
/// </summary>
public sealed class WabaProfileSyncOutbox : AggregateRoot<WabaProfileSyncOutboxId>
{
    private const int MaxLastErrorLength = 2000;

    private WabaProfileSyncOutbox(
        TenantId tenantId,
        string phoneNumberId,
        string requestedPayload,
        string? brandLogoUrl,
        DateTimeOffset nextAttemptAt
    ) : base(WabaProfileSyncOutboxId.NewId())
    {
        TenantId = tenantId;
        PhoneNumberId = phoneNumberId;
        RequestedPayload = requestedPayload;
        BrandLogoUrl = brandLogoUrl;
        Status = WabaProfileSyncStatus.Pending;
        Attempts = 0;
        NextAttemptAt = nextAttemptAt;
    }

    // Required by EF Core.
    private WabaProfileSyncOutbox() : base(WabaProfileSyncOutboxId.NewId())
    {
        RequestedPayload = string.Empty;
        PhoneNumberId = string.Empty;
    }

    public TenantId TenantId { get; private set; } = null!;

    public string PhoneNumberId { get; private set; }

    /// <summary>Serialized <c>WabaProfileDto</c> stored as <c>jsonb</c>.</summary>
    public string RequestedPayload { get; private set; }

    /// <summary>
    ///     URL of the blob backing the brand logo that the sync should upload to Meta. The Phase
    ///     7b job streams the blob bytes from storage, hashes them, and decides whether a new
    ///     resumable upload is required (compared to <see cref="LastSyncedLogoHash" />).
    ///     <see langword="null" /> when the tenant has no logo configured.
    /// </summary>
    public string? BrandLogoUrl { get; private set; }

    /// <summary>
    ///     Meta-issued upload handle returned by the second step of the Resumable Upload protocol.
    ///     Populated on transition to <see cref="WabaProfileSyncStatus.Posting" /> so the business
    ///     profile call can reference it via <c>profile_picture_handle</c>.
    /// </summary>
    public string? LogoUploadHandle { get; private set; }

    /// <summary>SHA-256 (hex) of the logo bytes last uploaded successfully to Meta.</summary>
    public string? LastSyncedLogoHash { get; private set; }

    public WabaProfileSyncStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? SyncedAt { get; private set; }

    public static WabaProfileSyncOutbox Enqueue(
        TenantId tenantId,
        string phoneNumberId,
        string requestedPayload,
        string? brandLogoUrl,
        DateTimeOffset nextAttemptAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedPayload);
        return new WabaProfileSyncOutbox(tenantId, phoneNumberId, requestedPayload, brandLogoUrl, nextAttemptAt);
    }

    public void MarkUploading()
    {
        EnsureTransitionAllowed(WabaProfileSyncStatus.Uploading, [WabaProfileSyncStatus.Pending]);
        Status = WabaProfileSyncStatus.Uploading;
        Attempts++;
    }

    public void MarkPosting(string uploadHandle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadHandle);
        EnsureTransitionAllowed(WabaProfileSyncStatus.Posting, [WabaProfileSyncStatus.Pending, WabaProfileSyncStatus.Uploading]);
        if (Status == WabaProfileSyncStatus.Pending)
        {
            // Skipping the upload step when the logo hasn't changed is allowed; the job still
            // counts the attempt because we did open the work envelope.
            Attempts++;
        }

        LogoUploadHandle = uploadHandle;
        Status = WabaProfileSyncStatus.Posting;
    }

    public void MarkSynced(string? newLogoHash, DateTimeOffset syncedAt)
    {
        EnsureTransitionAllowed(WabaProfileSyncStatus.Synced, [WabaProfileSyncStatus.Posting]);
        Status = WabaProfileSyncStatus.Synced;
        LastSyncedLogoHash = newLogoHash ?? LastSyncedLogoHash;
        SyncedAt = syncedAt;
        LastError = null;
        NextAttemptAt = null;
    }

    public void MarkFailed(string error, DateTimeOffset? nextAttemptAt)
    {
        EnsureTransitionAllowed(
            WabaProfileSyncStatus.Failed,
            [WabaProfileSyncStatus.Pending, WabaProfileSyncStatus.Uploading, WabaProfileSyncStatus.Posting]
        );
        Status = WabaProfileSyncStatus.Failed;
        LastError = Truncate(error, MaxLastErrorLength);
        NextAttemptAt = nextAttemptAt;
    }

    /// <summary>
    ///     Re-queues a previously failed row. The Phase 7b job calls this when the retry backoff
    ///     elapses so that the next polling cycle picks the row up again.
    /// </summary>
    public void Requeue(DateTimeOffset nextAttemptAt)
    {
        EnsureTransitionAllowed(WabaProfileSyncStatus.Pending, [WabaProfileSyncStatus.Failed]);
        Status = WabaProfileSyncStatus.Pending;
        NextAttemptAt = nextAttemptAt;
    }

    private void EnsureTransitionAllowed(WabaProfileSyncStatus target, WabaProfileSyncStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Cannot transition WabaProfileSyncOutbox from {Status} to {target}."
            );
        }
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }
}
