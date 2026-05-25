using Main.Features.Apps.Domain;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     <see cref="IExternalBusyTimeProvider" /> implementation backed by Google Calendar. Returns
///     an empty array when the user has no Google credential so the slot calculators can call
///     this for every host unconditionally.
/// </summary>
public sealed class GoogleCalendarBusyTimeProvider(
    ICredentialRepository credentialRepository,
    GoogleCalendarServiceFactory serviceFactory,
    ILogger<GoogleCalendarBusyTimeProvider> logger
) : IExternalBusyTimeProvider
{
    public async Task<IReadOnlyList<ExternalBusyTime>> GetBusyTimesAsync(
        TenantId tenantId,
        UserId userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    )
    {
        _ = tenantId;
        var credential = await credentialRepository.GetForUserAsync(userId, GoogleCalendarSlug.Slug, cancellationToken);
        if (credential is null) return [];

        try
        {
            var service = serviceFactory.Create(credential);
            return await service.GetBusyTimesAsync(from, to, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Slot calculation must remain available even if an external provider is down — log
            // and degrade to "no external busy times" rather than fail the booking flow.
            logger.LogWarning(
                exception, "Google Calendar busy-time lookup failed for user {UserId}; returning empty.", userId.Value
            );
            return [];
        }
    }
}
