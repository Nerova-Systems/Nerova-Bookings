using System.Security.Cryptography;
using System.Text.Json;
using Account.Database;
using Account.Features.Tenants.Domain;
using Account.Features.WhatsApp.Domain;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Cqrs;
using SharedKernel.Integrations.BlobStorage;
using SharedKernel.Telemetry;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Phase 7b outbox processor — processes a single <see cref="WabaProfileSyncOutbox" /> row to
///     completion (either <see cref="WabaProfileSyncStatus.Synced" /> or
///     <see cref="WabaProfileSyncStatus.Failed" />). Scoped service: the worker creates a fresh
///     scope (and therefore a fresh <see cref="AccountDbContext" />) per row so one row's
///     exception cannot poison the batch.
///     <para>
///         Persists twice in the happy path: once after <see cref="WabaProfileSyncOutbox.MarkPosting" />
///         (so the upload handle survives a crash before the POST), and once after
///         <see cref="WabaProfileSyncOutbox.MarkSynced" />. Telemetry is emitted AFTER
///         <see cref="AccountDbContext.SaveChangesAsync" /> so we never report state that did not
///         persist.
///     </para>
/// </summary>
public sealed class WabaProfileSyncProcessor(
    IWabaProfileSyncOutboxRepository outboxRepository,
    IWabaConfigurationRepository wabaConfigurationRepository,
    ITenantRepository tenantRepository,
    IWhatsAppCloudApiClient cloudApiClient,
    [FromKeyedServices("account-storage")] IBlobStorageClient blobStorageClient,
    IConfiguration configuration,
    AccountDbContext accountDbContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector telemetryEventsCollector,
    TelemetryClient telemetryClient,
    ILogger<WabaProfileSyncProcessor> logger
)
{
    // Backoff schedule: 1m, 5m, 30m, 2h. Index = (Attempts after the failed transition) - 1; values
    // beyond the schedule indicate a terminal failure (NextAttemptAt = null).
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2)
    ];

    /// <summary>
    ///     Processes the row identified by <paramref name="outboxId" />. Returns <see langword="true" />
    ///     if the row ended in <see cref="WabaProfileSyncStatus.Synced" />, otherwise
    ///     <see langword="false" />. Never throws — failures are recorded via
    ///     <see cref="WabaProfileSyncOutbox.MarkFailed" /> and surfaced as
    ///     <see cref="WabaProfileSyncFailed" /> telemetry.
    /// </summary>
    public async Task<bool> ProcessAsync(WabaProfileSyncOutboxId outboxId, CancellationToken cancellationToken)
    {
        var row = await outboxRepository.GetByIdAsync(outboxId, cancellationToken);
        if (row is null)
        {
            logger.LogWarning("WabaProfileSyncOutbox row '{Id}' not found; skipping", outboxId);
            return false;
        }

        // A Failed row whose backoff has elapsed needs to be requeued before we can advance it.
        if (row.Status == WabaProfileSyncStatus.Failed)
        {
            if (row.Attempts >= WabaProfileSyncOutbox.MaxAttempts)
            {
                // Terminally failed — should not have been picked up. Defensive no-op.
                return false;
            }

            row.Requeue(timeProvider.GetUtcNow());
        }

        try
        {
            var success = await ProcessCoreAsync(row, cancellationToken);
            if (success)
            {
                telemetryEventsCollector.CollectEvent(
                    new WabaProfileSyncSucceeded(row.TenantId, row.PhoneNumberId, row.Attempts)
                );
            }

            await accountDbContext.SaveChangesAsync(cancellationToken);
            DrainTelemetry();
            return success;
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(row, ex, cancellationToken);
            return false;
        }
    }

    private async Task<bool> ProcessCoreAsync(WabaProfileSyncOutbox row, CancellationToken cancellationToken)
    {
        var waba = await wabaConfigurationRepository.GetByTenantIdAsync(row.TenantId, cancellationToken);
        if (waba?.PhoneNumberId is null || waba.WabaAccessToken is null)
        {
            // Tenant unlinked between enqueue and processing — there's nothing to post. Mark
            // terminally failed so we don't keep retrying forever.
            row.MarkFailed("Tenant is no longer linked to a WABA.", nextAttemptAt: null);
            return false;
        }

        var tenants = await tenantRepository.GetByIdsUnfilteredAsync([row.TenantId], cancellationToken);
        var tenant = tenants.FirstOrDefault();
        if (tenant?.BrandProfile is null)
        {
            row.MarkFailed("Tenant or BrandProfile no longer exists.", nextAttemptAt: null);
            return false;
        }

        // ─── Step 1: logo upload (if any) ────────────────────────────────
        string? uploadHandle = null;
        string? newLogoHash = null;
        if (row.BrandLogoUrl is not null)
        {
            var appId = configuration["WhatsApp:MetaAppId"];
            if (string.IsNullOrWhiteSpace(appId))
            {
                row.MarkFailed("WhatsApp:MetaAppId is not configured.", nextAttemptAt: null);
                return false;
            }

            var (bytes, contentType) = await FetchLogoAsync(row.BrandLogoUrl, cancellationToken);
            newLogoHash = ComputeSha256(bytes);

            // Skip re-uploading bytes that Meta already has. We still need a handle for the POST
            // so we go through MarkPosting only when we actually upload; if the hash is unchanged
            // we transition straight to Posting via MarkPostingWithoutLogo (handle stays null and
            // the POST omits profile_picture_handle, leaving Meta's existing picture untouched).
            if (newLogoHash != row.LastSyncedLogoHash)
            {
                row.MarkUploading();
                await accountDbContext.SaveChangesAsync(cancellationToken);

                var uploadResult = await cloudApiClient.UploadProfilePictureAsync(
                    appId, waba.WabaAccessToken, bytes, contentType, cancellationToken
                );
                if (!uploadResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Meta resumable upload failed: {uploadResult.GetErrorSummary()}"
                    );
                }

                uploadHandle = uploadResult.Value
                    ?? throw new InvalidOperationException("Meta resumable upload returned a null handle.");
                row.MarkPosting(uploadHandle);
                // Persist the handle separately so a crash before the POST doesn't lose it.
                await accountDbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                row.MarkPostingWithoutLogo();
            }
        }
        else
        {
            row.MarkPostingWithoutLogo();
        }

        // ─── Step 2: post the business profile ───────────────────────────
        var dto = WabaProfileMapper.Map(tenant.BrandProfile, uploadHandle);
        var serializedPayload = JsonSerializer.Serialize(dto);
        var postResult = await cloudApiClient.UpdateBusinessProfileAsync(
            waba.PhoneNumberId, waba.WabaAccessToken, serializedPayload, cancellationToken
        );
        if (!postResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Meta business profile POST failed: {postResult.GetErrorSummary()}"
            );
        }

        row.MarkSynced(newLogoHash, timeProvider.GetUtcNow());
        return true;
    }

    private async Task HandleFailureAsync(WabaProfileSyncOutbox row, Exception ex, CancellationToken cancellationToken)
    {
        // Discard any pending dirty state so we can re-mutate the row cleanly. The row is the only
        // entity we touch in the scope, so clearing the change-tracker is safe.
        accountDbContext.ChangeTracker.Clear();
        var reload = await outboxRepository.GetByIdAsync(row.Id, cancellationToken);
        if (reload is null) return;

        var nextAttemptAt = ComputeNextAttempt(reload.Attempts + 1);
        var terminal = nextAttemptAt is null;
        try
        {
            reload.MarkFailed(ex.Message, nextAttemptAt);
        }
        catch (InvalidOperationException markFailedEx)
        {
            // Row is already in a terminal state we cannot transition from. Log and bail.
            logger.LogWarning(markFailedEx, "Could not mark WabaProfileSyncOutbox '{Id}' as Failed", row.Id);
            return;
        }

        try
        {
            await accountDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveEx)
        {
            logger.LogError(saveEx, "Failed to persist Failed state for WabaProfileSyncOutbox '{Id}'", row.Id);
            return;
        }

        telemetryEventsCollector.CollectEvent(
            new WabaProfileSyncFailed(reload.TenantId, reload.PhoneNumberId, reload.Attempts, Truncate(ex.Message, 500), terminal)
        );
        DrainTelemetry();
    }

    private async Task<(byte[] Bytes, string ContentType)> FetchLogoAsync(string brandLogoUrl, CancellationToken cancellationToken)
    {
        // Stored URL shape: "/{container}/{tenantId}/logo/{hash}.{ext}". Strip leading slash and
        // split the first segment off as the container.
        var trimmed = brandLogoUrl.StartsWith('/') ? brandLogoUrl[1..] : brandLogoUrl;
        var firstSlash = trimmed.IndexOf('/');
        if (firstSlash <= 0 || firstSlash >= trimmed.Length - 1)
        {
            throw new InvalidOperationException($"Unrecognized BrandLogoUrl shape: '{brandLogoUrl}'");
        }

        var container = trimmed[..firstSlash];
        var blobName = trimmed[(firstSlash + 1)..];

        var download = await blobStorageClient.DownloadAsync(container, blobName, cancellationToken);
        if (download is null)
        {
            throw new InvalidOperationException($"Brand logo blob not found: '{brandLogoUrl}'");
        }

        await using var stream = download.Value.Stream;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return (memory.ToArray(), download.Value.ContentType);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private DateTimeOffset? ComputeNextAttempt(int nextAttempt)
    {
        // nextAttempt is the count *after* the imminent MarkFailed bump; a row that has just had
        // its 5th attempt fail has no schedule left.
        var index = nextAttempt - 1;
        if (index < 0 || index >= BackoffSchedule.Length)
        {
            return null;
        }

        return timeProvider.GetUtcNow().Add(BackoffSchedule[index]);
    }

    private void DrainTelemetry()
    {
        while (telemetryEventsCollector.HasEvents)
        {
            var evt = telemetryEventsCollector.Dequeue();
            telemetryClient.TrackEvent(evt.GetType().Name, evt.Properties);
        }
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }
}

