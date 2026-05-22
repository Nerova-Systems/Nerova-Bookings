using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
public sealed record GetBookingAttendeesQuery(BookingId Id) : IRequest<Result<BookingAttendeesResponse>>;

[PublicAPI]
public sealed record BookingAttendeesResponse(BookingAttendeeResponse[] Attendees);

public sealed class GetBookingAttendeesHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetBookingAttendeesQuery, Result<BookingAttendeesResponse>>
{
    public async Task<Result<BookingAttendeesResponse>> Handle(GetBookingAttendeesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingAttendeesResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, query.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingAttendeesResponse>.NotFound($"Booking '{query.Id}' was not found.");
        }

        var attendees = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        return new BookingAttendeesResponse(
            attendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray()
        );
    }
}
