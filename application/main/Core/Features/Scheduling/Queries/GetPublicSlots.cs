using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

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
    IHostRepository hostRepository,
    ITravelScheduleRepository travelScheduleRepository,
    IOutOfOfficeRepository outOfOfficeRepository,
    PublicSlotCalculator publicSlotCalculator,
    CollectiveSlotCalculator collectiveSlotCalculator,
    RoundRobinSlotCalculator roundRobinSlotCalculator
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

        // Pre-fetch schedule-owner adjustments (TravelSchedule + OutOfOffice) covering the request range.
        // Use a one-day buffer on either side to match the booking pre-fetch window.
        var adjustmentsRangeStart = DateOnly.FromDateTime(query.StartTime.UtcDateTime).AddDays(-1);
        var adjustmentsRangeEnd = DateOnly.FromDateTime(query.EndTime.UtcDateTime).AddDays(1);
        var travelSchedules = await travelScheduleRepository.GetActiveForUserUnfilteredAsync(
            context.Profile.TenantId, context.Profile.OwnerUserId, adjustmentsRangeStart, adjustmentsRangeEnd, cancellationToken
        );
        var outOfOffices = await outOfOfficeRepository.GetActiveForUserUnfilteredAsync(
            context.Profile.TenantId, context.Profile.OwnerUserId, adjustmentsRangeStart, adjustmentsRangeEnd, cancellationToken
        );
        var adjustments = new ScheduleAdjustments(travelSchedules, outOfOffices);

        Dictionary<string, PublicSlotResponse[]> slots;

        if (context.EventType.SchedulingType == SchedulingType.Collective)
        {
            var hosts = await hostRepository.GetForEventTypeUnfilteredAsync(context.EventType.Id, cancellationToken);
            var hostUserIds = hosts.Select(h => h.UserId).ToList();
            var hostBookings = hostUserIds.Count > 0
                ? await bookingRepository.GetForMultipleOwnersRangeAsync(
                    context.Profile.TenantId,
                    hostUserIds,
                    query.StartTime.AddDays(-1),
                    query.EndTime.AddDays(1),
                    cancellationToken
                )
                : new Dictionary<UserId, Booking[]>();

            slots = collectiveSlotCalculator.GetSlots(context.EventType, context.Schedule, hostBookings, query.StartTime, query.EndTime, query.TimeZone, duration, adjustments);
        }
        else if (context.EventType.SchedulingType == SchedulingType.RoundRobin)
        {
            var hosts = await hostRepository.GetForEventTypeUnfilteredAsync(context.EventType.Id, cancellationToken);
            var hostUserIds = hosts.Select(h => h.UserId).ToList();
            var hostBookings = hostUserIds.Count > 0
                ? await bookingRepository.GetForMultipleOwnersRangeAsync(
                    context.Profile.TenantId,
                    hostUserIds,
                    query.StartTime.AddDays(-1),
                    query.EndTime.AddDays(1),
                    cancellationToken
                )
                : new Dictionary<UserId, Booking[]>();

            slots = roundRobinSlotCalculator.GetSlots(context.EventType, context.Schedule, hostBookings, hosts, query.StartTime, query.EndTime, query.TimeZone, duration, adjustments);
        }
        else
        {
            var bookings = await bookingRepository.GetForOwnerRangeUnfilteredAsync(
                context.Profile.TenantId,
                context.Profile.OwnerUserId,
                query.StartTime.AddDays(-1),
                query.EndTime.AddDays(1),
                cancellationToken
            );
            slots = publicSlotCalculator.GetSlots(context.EventType, context.Schedule, bookings, query.StartTime, query.EndTime, query.TimeZone, duration, adjustments);
        }

        return new PublicSlotsResponse(slots);
    }
}
