using JetBrains.Annotations;
using Main.Features.Connectors.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
public sealed record GetPublicSlotsQuery(
    string Handle,
    string EventSlug,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string TimeZone,
    int? Duration = null,
    string? PrivateLink = null
) : IRequest<Result<PublicSlotsResponse>>;

public sealed class GetPublicSlotsHandler(
    PublicSchedulingResolver publicSchedulingResolver,
    IBookingRepository bookingRepository,
    PublicSlotCalculator publicSlotCalculator,
    ICoreConnectorClient coreConnectorClient
) : IRequestHandler<GetPublicSlotsQuery, Result<PublicSlotsResponse>>
{
    public async Task<Result<PublicSlotsResponse>> Handle(GetPublicSlotsQuery query, CancellationToken cancellationToken)
    {
        if (query.EndTime <= query.StartTime)
        {
            return Result<PublicSlotsResponse>.BadRequest("End time must be after start time.");
        }

        var contextResult = await publicSchedulingResolver.ResolveAsync(query.Handle, query.EventSlug, query.PrivateLink, cancellationToken);
        if (!contextResult.IsSuccess)
        {
            return Result<PublicSlotsResponse>.From(contextResult);
        }

        var context = contextResult.Value!;
        var duration = query.Duration ?? context.EventType.DurationMinutes;
        if (!context.EventType.DurationOptions.Contains(duration))
        {
            return Result<PublicSlotsResponse>.BadRequest("Duration is not available for this event type.");
        }

        var bookings = await bookingRepository.GetForOwnerRangeUnfilteredAsync(
            context.Profile.TenantId,
            context.Profile.OwnerUserId,
            query.StartTime.AddDays(-1),
            query.EndTime.AddDays(1),
            cancellationToken
        );
        var busyWindows = await coreConnectorClient.GetBusyWindowsAsync(
            context.EventType.Settings.SelectedCalendars,
            query.StartTime,
            query.EndTime,
            cancellationToken
        );
        var slots = publicSlotCalculator.GetSlots(context.EventType, context.Schedule, bookings, busyWindows, query.StartTime, query.EndTime, query.TimeZone, duration);

        return new PublicSlotsResponse(slots);
    }
}
