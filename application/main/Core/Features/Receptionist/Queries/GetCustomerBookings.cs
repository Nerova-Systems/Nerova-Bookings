using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Receptionist.Queries;

[PublicAPI]
public sealed record CustomerBookingResponse(
    BookingId Id,
    string BookingCode,
    string ServiceTitle,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    string PaymentStatus,
    string? PaymentLinkUrl
);

[PublicAPI]
public sealed record GetCustomerBookingsResponse(CustomerBookingResponse[] Bookings);

/// <summary>
///     The upcoming bookings of a WhatsApp customer, identified by their verified phone number from
///     server-side conversation state. Bookings are exposed to the model as short codes (the tail of the
///     booking id) — never raw identifiers the model could swap for another customer's (spec §6.4).
/// </summary>
[PublicAPI]
public sealed record GetCustomerBookingsQuery(TenantId TenantId, string CustomerPhoneNumber) : IRequest<Result<GetCustomerBookingsResponse>>;

public sealed class GetCustomerBookingsHandler(IBookingRepository bookingRepository, IEventTypeRepository eventTypeRepository, TimeProvider timeProvider)
    : IRequestHandler<GetCustomerBookingsQuery, Result<GetCustomerBookingsResponse>>
{
    public async Task<Result<GetCustomerBookingsResponse>> Handle(GetCustomerBookingsQuery query, CancellationToken cancellationToken)
    {
        var bookings = await bookingRepository.GetUpcomingByBookerPhoneUnfilteredAsync(query.TenantId, query.CustomerPhoneNumber, timeProvider.GetUtcNow(), cancellationToken);
        if (bookings.Length == 0)
        {
            return Result<GetCustomerBookingsResponse>.Success(new GetCustomerBookingsResponse([]));
        }

        var eventTypes = await eventTypeRepository.GetByIdsAsync([.. bookings.Select(booking => booking.EventTypeId).Distinct()], cancellationToken);
        var eventTypesById = eventTypes.ToDictionary(eventType => eventType.Id);

        var responses = bookings.Select(booking => new CustomerBookingResponse(
                booking.Id,
                ToBookingCode(booking.Id),
                eventTypesById.TryGetValue(booking.EventTypeId, out var eventType) ? eventType.Title : booking.Title,
                booking.StartTime,
                booking.EndTime,
                booking.Status.ToString(),
                booking.PaymentStatus.ToString(),
                booking.PaymentLinkUrl
            )
        ).ToArray();

        return Result<GetCustomerBookingsResponse>.Success(new GetCustomerBookingsResponse(responses));
    }

    /// <summary>Conversation-scoped short code for a booking: the last six characters of its ULID.</summary>
    public static string ToBookingCode(BookingId bookingId)
    {
        var value = bookingId.Value;
        return value.Length <= 6 ? value.ToUpperInvariant() : value[^6..].ToUpperInvariant();
    }
}
