using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Domain;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     <see cref="IExternalBusyTimeProvider" /> implementation backed by Microsoft Office 365
///     Calendar (Outlook). Returns an empty array when the user has no Office 365 credential
///     so the slot calculators can call this for every host unconditionally.
///     <para>
///         The interface itself lives alongside the Google connector (it was introduced by
///         the T2-google-cal track first); this implementation simply registers a second
///         provider against it so slot calculators iterate <c>IEnumerable&lt;IExternalBusyTimeProvider&gt;</c>
///         without branching per integration.
///     </para>
/// </summary>
public sealed class Office365CalendarBusyTimeProvider(
    ICredentialRepository credentialRepository,
    Office365CalendarServiceFactory serviceFactory,
    ILogger<Office365CalendarBusyTimeProvider> logger
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
        var credential = await credentialRepository.GetForUserAsync(userId, Office365CalendarSlug.Slug, cancellationToken);
        if (credential is null) return [];

        try
        {
            var service = serviceFactory.Create(credential);
            return await service.GetBusyTimesAsync(from, to, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Slot calculation must remain available even if an external provider is down —
            // log and degrade to "no external busy times" rather than fail the booking flow.
            logger.LogWarning(
                exception, "Office 365 Calendar busy-time lookup failed for user {UserId}; returning empty.", userId.Value
            );
            return [];
        }
    }
}
