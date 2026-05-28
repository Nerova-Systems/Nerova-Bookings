using Account.Database;
using Account.Features.Tenants.Domain;
using Account.Features.WhatsApp.Domain;
using Microsoft.ApplicationInsights;
using SharedKernel.Telemetry;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Phase 7c poller — scans every WABA configuration in
///     <see cref="WabaDisplayNameStatus.PendingReview" />, asks Meta for the current
///     <c>name_status</c>, and applies the result through
///     <see cref="WabaConfiguration.MarkDisplayNameReviewResult" />.
///     <para>
///         Telemetry is emitted AFTER <see cref="AccountDbContext.SaveChangesAsync" /> so we
///         never report a transition that did not persist. A single Meta failure for one
///         configuration does not poison the batch — the loop catches and logs per row.
///     </para>
/// </summary>
public sealed class WabaDisplayNameReviewPoller(
    IWabaConfigurationRepository wabaConfigurationRepository,
    ITenantRepository tenantRepository,
    IWhatsAppCloudApiClient cloudApiClient,
    AccountDbContext accountDbContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector telemetryEventsCollector,
    TelemetryClient telemetryClient,
    ILogger<WabaDisplayNameReviewPoller> logger
)
{
    public async Task PollAsync(CancellationToken cancellationToken)
    {
        var configs = await wabaConfigurationRepository.GetAllPendingDisplayNameReviewAsync(cancellationToken);
        if (configs.Count == 0)
        {
            logger.LogInformation("Display-name review poller found no pending reviews");
            return;
        }

        var transitioned = 0;
        foreach (var waba in configs)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (waba.PhoneNumberId is null || waba.WabaAccessToken is null) continue;

            try
            {
                var statusResult = await cloudApiClient.GetDisplayNameStatusAsync(
                    waba.PhoneNumberId, waba.WabaAccessToken, cancellationToken
                );
                if (!statusResult.IsSuccess || statusResult.Value is null)
                {
                    logger.LogWarning(
                        "Display-name status fetch failed for tenant '{TenantId}': {Error}",
                        waba.TenantId, statusResult.GetErrorSummary()
                    );
                    continue;
                }

                var remote = statusResult.Value;
                var previous = waba.DisplayNameStatus;
                waba.MarkDisplayNameReviewResult(remote.NameStatus, remote.VerifiedName, timeProvider.GetUtcNow());

                if (waba.DisplayNameStatus != previous)
                {
                    var requested = waba.RequestedDisplayName ?? string.Empty;
                    switch (waba.DisplayNameStatus)
                    {
                        case WabaDisplayNameStatus.Approved:
                            telemetryEventsCollector.CollectEvent(new WabaDisplayNameApproved(
                                waba.TenantId, waba.PhoneNumberId, remote.VerifiedName ?? requested
                            ));
                            var tenant = await tenantRepository.GetByIdUnfilteredAsync(waba.TenantId, cancellationToken);
                            if (tenant is not null)
                            {
                                var updatedProfile = waba.TrySyncVerifiedNameToBrandProfile(tenant.BrandProfile);
                                if (updatedProfile is not null)
                                {
                                    tenant.UpdateBrandProfile(updatedProfile);
                                    tenantRepository.Update(tenant);
                                }
                            }

                            break;
                        case WabaDisplayNameStatus.Declined:
                            telemetryEventsCollector.CollectEvent(new WabaDisplayNameDeclined(
                                waba.TenantId, waba.PhoneNumberId, requested
                            ));
                            break;
                        case WabaDisplayNameStatus.Expired:
                            telemetryEventsCollector.CollectEvent(new WabaDisplayNameExpired(
                                waba.TenantId, waba.PhoneNumberId, requested
                            ));
                            break;
                    }

                    transitioned++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Display-name review poller failed for tenant '{TenantId}'", waba.TenantId);
            }
        }

        await accountDbContext.SaveChangesAsync(cancellationToken);

        // Drain AFTER commit so we never emit transition telemetry for rows that didn't persist.
        while (telemetryEventsCollector.HasEvents)
        {
            var evt = telemetryEventsCollector.Dequeue();
            telemetryClient.TrackEvent(evt.GetType().Name, evt.Properties);
        }

        logger.LogInformation(
            "Display-name review poller completed: {Scanned} pending, {Transitioned} transitioned",
            configs.Count, transitioned
        );
    }
}
