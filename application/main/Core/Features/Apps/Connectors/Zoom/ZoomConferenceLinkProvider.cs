using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     Adapts the connector-specific <see cref="ZoomService" /> to the generic
///     <see cref="IConferenceLinkProvider" /> contract the scheduling layer consumes. Per
///     credential the provider builds a fresh service via <see cref="ZoomServiceFactory" />
///     so token-refresh persistence flows through the request-scoped repository.
/// </summary>
public sealed class ZoomConferenceLinkProvider(ZoomServiceFactory factory) : IConferenceLinkProvider
{
    public AppSlug Slug => ZoomSlug.Slug;

    public Task<ConferenceLink> CreateAsync(Credential credential, BookingEvent input, CancellationToken cancellationToken)
    {
        return factory.Create(credential).CreateMeetingAsync(input, cancellationToken);
    }

    public Task<ConferenceLink> UpdateAsync(Credential credential, string externalId, BookingEvent input, CancellationToken cancellationToken)
    {
        return factory.Create(credential).UpdateMeetingAsync(externalId, input, cancellationToken);
    }

    public Task CancelAsync(Credential credential, string externalId, CancellationToken cancellationToken)
    {
        return factory.Create(credential).CancelMeetingAsync(externalId, cancellationToken);
    }
}
