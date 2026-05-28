using Account.Database;
using Account.Features.Tenants.Domain;
using Account.Features.WhatsApp.Domain;
using Microsoft.ApplicationInsights;
using SharedKernel.Telemetry;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Phase 7b drift detector — runs once per day, scans every linked WABA configuration, asks
///     Meta for the current <c>whatsapp_business_profile</c>, and enqueues a sync row whenever the
///     remote shape diverges from the tenant's local <see cref="BrandProfile" />. Idempotent: if a
///     non-terminal sync row already exists for the tenant, the detector skips so we never stack
///     drift-driven rows on top of in-flight retries.
/// </summary>
public sealed class WabaProfileDriftDetector(
    IWabaConfigurationRepository wabaConfigurationRepository,
    IWabaProfileSyncOutboxRepository outboxRepository,
    ITenantRepository tenantRepository,
    IWhatsAppCloudApiClient cloudApiClient,
    AccountDbContext accountDbContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector telemetryEventsCollector,
    TelemetryClient telemetryClient,
    ILogger<WabaProfileDriftDetector> logger
)
{
    public async Task DetectAsync(CancellationToken cancellationToken)
    {
        var configs = await wabaConfigurationRepository.GetAllLinkedAsync(cancellationToken);
        if (configs.Count == 0)
        {
            logger.LogInformation("Drift detector found no linked WABA configurations");
            return;
        }

        var tenantIds = configs.Select(c => c.TenantId).Distinct().ToArray();
        var tenants = await tenantRepository.GetByIdsUnfilteredAsync(tenantIds, cancellationToken);
        var tenantsById = tenants.ToDictionary(t => t.Id);

        var enqueued = 0;
        foreach (var waba in configs)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (waba.PhoneNumberId is null || waba.WabaAccessToken is null) continue;

            try
            {
                if (!tenantsById.TryGetValue(waba.TenantId, out var tenant) || tenant.BrandProfile is null)
                {
                    continue;
                }

                if (await outboxRepository.HasNonTerminalForTenantAsync(waba.TenantId, cancellationToken))
                {
                    // A sync is already pending/retrying — skip to keep the outbox idempotent.
                    continue;
                }

                var remoteResult = await cloudApiClient.GetBusinessProfileAsync(
                    waba.PhoneNumberId, waba.WabaAccessToken, cancellationToken
                );
                if (!remoteResult.IsSuccess || remoteResult.Value is null)
                {
                    logger.LogWarning(
                        "Drift detector could not read Meta profile for tenant '{TenantId}': {Error}",
                        waba.TenantId, remoteResult.GetErrorSummary()
                    );
                    continue;
                }

                var expected = WabaProfileMapper.Map(tenant.BrandProfile, profilePictureHandle: null);
                var drifted = DiffFields(expected, remoteResult.Value, tenant.BrandProfile.BrandLogoUrl);
                if (drifted.Length == 0) continue;

                var serialized = System.Text.Json.JsonSerializer.Serialize(expected);
                var outbox = WabaProfileSyncOutbox.Enqueue(
                    waba.TenantId,
                    waba.PhoneNumberId,
                    serialized,
                    tenant.BrandProfile.BrandLogoUrl,
                    timeProvider.GetUtcNow()
                );
                await outboxRepository.AddAsync(outbox, cancellationToken);
                telemetryEventsCollector.CollectEvent(
                    new WabaProfileDriftDetected(waba.TenantId, waba.PhoneNumberId, string.Join(",", drifted))
                );
                enqueued++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Drift detector failed for tenant '{TenantId}'", waba.TenantId);
            }
        }

        await accountDbContext.SaveChangesAsync(cancellationToken);

        // Drain AFTER commit so we never emit drift telemetry for rows that didn't persist.
        while (telemetryEventsCollector.HasEvents)
        {
            var evt = telemetryEventsCollector.Dequeue();
            telemetryClient.TrackEvent(evt.GetType().Name, evt.Properties);
        }

        logger.LogInformation(
            "Drift detector completed: {Scanned} configs scanned, {Enqueued} sync rows enqueued",
            configs.Count, enqueued
        );
    }

    /// <summary>
    ///     Returns the list of field names that differ between the expected (locally-mapped) DTO
    ///     and Meta's current profile. The profile picture is compared by presence only — we
    ///     cannot compare hashes against Meta's hosted URL, so we treat any change in null-ness
    ///     as drift on the <c>profile_picture</c> field.
    /// </summary>
    public static string[] DiffFields(WabaProfileDto expected, RemoteWabaProfileDto remote, string? localBrandLogoUrl)
    {
        var diffs = new List<string>();

        if (!string.Equals(expected.About, remote.About, StringComparison.Ordinal)) diffs.Add("about");
        if (!string.Equals(expected.Address, remote.Address, StringComparison.Ordinal)) diffs.Add("address");
        if (!string.Equals(expected.Description, remote.Description, StringComparison.Ordinal)) diffs.Add("description");
        if (!string.Equals(expected.Email, remote.Email, StringComparison.Ordinal)) diffs.Add("email");
        if (!string.Equals(expected.Vertical, remote.Vertical, StringComparison.Ordinal)) diffs.Add("vertical");

        var expectedSites = expected.Websites ?? [];
        var remoteSites = remote.Websites ?? [];
        if (!expectedSites.SequenceEqual(remoteSites, StringComparer.Ordinal)) diffs.Add("websites");

        var localHasLogo = !string.IsNullOrWhiteSpace(localBrandLogoUrl);
        var remoteHasLogo = !string.IsNullOrWhiteSpace(remote.ProfilePictureUrl);
        if (localHasLogo != remoteHasLogo) diffs.Add("profile_picture");

        return diffs.ToArray();
    }
}
