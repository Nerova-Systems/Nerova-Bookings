using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record ReleaseBookingSeatCommand(BookingId Id, BookingSeatId SeatId) : ICommand, IRequest<Result>;

public sealed class ReleaseBookingSeatHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingSeatRepository bookingSeatRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<ReleaseBookingSeatCommand, Result>
{
    public async Task<Result> Handle(ReleaseBookingSeatCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        var seat = await bookingSeatRepository.GetByIdAsync(command.SeatId, cancellationToken);
        if (seat is null || seat.BookingId != item.Booking.Id)
        {
            return Result.NotFound($"Seat '{command.SeatId}' was not found.");
        }

        var attendee = await bookingAttendeeRepository.GetByIdAsync(seat.AttendeeId, cancellationToken);
        bookingSeatRepository.Remove(seat);
        if (attendee is not null)
        {
            bookingAttendeeRepository.Remove(attendee);
        }

        var payload = JsonSerializer.Serialize(new { seatId = seat.Id.Value });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.SeatReleased,
            timeProvider.GetUtcNow(),
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        return Result.Success();
    }
}
