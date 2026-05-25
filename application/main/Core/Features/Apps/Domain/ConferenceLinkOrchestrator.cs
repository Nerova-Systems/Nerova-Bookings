using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Apps.Domain;

/// <summary>
///     Bridges the scheduling layer to the registered conferencing connectors. The booking
///     command handlers call <see cref="ApplyAsync" /> after a <see cref="Booking" /> is
///     persisted: if the resolved <see cref="EventType" /> declares an
///     <c>integration:&lt;slug&gt;</c> location and the owner has the matching credential,
///     the orchestrator asks the provider to create the meeting, writes the join URL onto the
///     booking, and records a <see cref="BookingReference" /> so update/cancel can later
///     target the same upstream record.
///     <para>
///         Failures are intentionally non-fatal: a missing credential, an unsupported slug, or
///         a provider HTTP error logs a warning but never blocks the booking from being
///         confirmed. The booking still saves and the user receives the local confirmation;
///         operators can then re-install the credential and retry the meeting creation out of
///         band. This mirrors cal.com's "soft-fail conferencing" stance.
///     </para>
/// </summary>
public sealed class ConferenceLinkOrchestrator(
    IEnumerable<IConferenceLinkProvider> providers,
    ICredentialRepository credentialRepository,
    IBookingReferenceRepository bookingReferenceRepository,
    ILogger<ConferenceLinkOrchestrator> logger
)
{
    public async Task ApplyAsync(
        Booking booking,
        EventType eventType,
        TenantId tenantId,
        UserId ownerUserId,
        CancellationToken cancellationToken
    )
    {
        var slug = ResolveConferencingSlug(eventType);
        if (slug is null) return;

        var provider = providers.FirstOrDefault(p => p.Slug == slug);
        if (provider is null)
        {
            logger.LogWarning(
                "Booking {BookingId} requested conferencing app '{Slug}' but no provider is registered; booking will save without a join URL.",
                booking.Id.Value, slug.Value
            );
            return;
        }

        var credential = await credentialRepository.GetForUserAsync(ownerUserId, slug, cancellationToken);
        if (credential is null)
        {
            logger.LogWarning(
                "Owner {OwnerUserId} has no '{Slug}' credential installed; booking {BookingId} will save without a join URL.",
                ownerUserId.Value, slug.Value, booking.Id.Value
            );
            return;
        }

        var input = new BookingEvent(
            eventType.Title,
            $"Booking with {booking.BookerName}",
            booking.StartTime,
            booking.EndTime,
            booking.TimeZone,
            booking.BookerEmail,
            booking.BookerName,
            [new BookingEventAttendee(booking.BookerEmail, booking.BookerName)]
        );

        ConferenceLink link;
        try
        {
            link = await provider.CreateAsync(credential, input, cancellationToken);
        }
        catch (Exception exception)
        {
            // Non-fatal: log and let the booking persist without a join URL. Operators can
            // re-attempt manually; cancel/reschedule will skip the provider call because no
            // BookingReference was written.
            logger.LogError(
                exception,
                "Provider '{Slug}' failed to create a meeting for booking {BookingId}; booking will save without a join URL.",
                slug.Value, booking.Id.Value
            );
            return;
        }

        booking.SetLocation("integration", link.JoinUrl);
        var reference = BookingReference.Create(tenantId, booking.Id, slug, link.ExternalId, link.JoinUrl);
        await bookingReferenceRepository.AddAsync(reference, cancellationToken);
    }

    /// <summary>
    ///     Returns the app slug the event type expects a conferencing meeting from, or
    ///     <c>null</c> if the event type uses a non-integration location (link, in-person, …)
    ///     or has no location set. Recognises the top-level <c>LocationType="integration"</c>
    ///     pair as the canonical signal — the <c>Settings.Locations[]</c> array is informational
    ///     (display ordering, attendee-defined fallbacks) and is not used to drive provider
    ///     selection.
    /// </summary>
    internal static AppSlug? ResolveConferencingSlug(EventType eventType)
    {
        var type = eventType.LocationType;
        var value = eventType.LocationValue;
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value)) return null;
        if (!string.Equals(type, "integration", StringComparison.OrdinalIgnoreCase)) return null;
        return new AppSlug(value.Trim().ToLowerInvariant());
    }
}
