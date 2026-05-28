using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Reserve a seat on a seated booking for a new attendee. Capacity is enforced against
///     <c>EventType.Settings.Seats.Capacity</c>. Reference UID is auto-generated.
/// </summary>
[PublicAPI]
public sealed record ReserveBookingSeatCommand(BookingId Id, string AttendeeName, string AttendeeEmail, string TimeZone, string? Locale = null) : ICommand, IRequest<Result<ReserveBookingSeatResponse>>;

[PublicAPI]
public sealed record ReserveBookingSeatResponse(BookingSeatId SeatId, string ReferenceUid);

public sealed class ReserveBookingSeatValidator : AbstractValidator<ReserveBookingSeatCommand>
{
    public ReserveBookingSeatValidator()
    {
        RuleFor(command => command.AttendeeName).NotEmpty().MaximumLength(120);
        RuleFor(command => command.AttendeeEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Locale).MaximumLength(20);
    }
}

public sealed class ReserveBookingSeatHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingSeatRepository bookingSeatRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<ReserveBookingSeatCommand, Result<ReserveBookingSeatResponse>>
{
    public async Task<Result<ReserveBookingSeatResponse>> Handle(ReserveBookingSeatCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<ReserveBookingSeatResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result<ReserveBookingSeatResponse>.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result<ReserveBookingSeatResponse>.BadRequest("Closed bookings cannot accept new seats.");
        }

        var seats = item.EventType.Settings.Seats;
        if (!seats.Enabled)
        {
            return Result<ReserveBookingSeatResponse>.BadRequest("Seats are not enabled for this event type.");
        }

        var existing = await bookingSeatRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        if (seats.Capacity is not null && existing.Length >= seats.Capacity.Value)
        {
            return Result<ReserveBookingSeatResponse>.BadRequest("This booking is at full seat capacity.");
        }

        var attendee = BookingAttendee.Create(tenantId, item.Booking.Id, command.AttendeeName, command.AttendeeEmail, command.TimeZone, command.Locale ?? string.Empty);
        await bookingAttendeeRepository.AddAsync(attendee, cancellationToken);

        var referenceUid = $"{item.Booking.Id.Value}-{Guid.NewGuid():N}";
        var seat = BookingSeat.Create(tenantId, item.Booking.Id, attendee.Id, referenceUid);
        await bookingSeatRepository.AddAsync(seat, cancellationToken);

        var payload = JsonSerializer.Serialize(new { seatId = seat.Id.Value, attendeeEmail = attendee.Email });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.SeatReserved,
            timeProvider.GetUtcNow(),
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        return new ReserveBookingSeatResponse(seat.Id, seat.ReferenceUid);
    }
}
