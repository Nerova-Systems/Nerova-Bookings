using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Marks the host or a specific attendee as no-show. Idempotent: re-sending the same value
///     succeeds without changing state.
/// </summary>
[PublicAPI]
public sealed record MarkNoShowCommand(BookingId Id, BookingAttendeeId? AttendeeId, bool NoShow) : ICommand, IRequest<Result>;

public sealed class MarkNoShowHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<MarkNoShowCommand, Result>
{
    public async Task<Result> Handle(MarkNoShowCommand command, CancellationToken cancellationToken)
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

        var now = timeProvider.GetUtcNow();
        if (item.Booking.EndTime > now)
        {
            return Result.BadRequest("No-show can only be recorded after the booking has ended.");
        }

        string subject;
        if (command.AttendeeId is null)
        {
            if (item.Booking.NoShowHost == command.NoShow)
            {
                return Result.Success();
            }
            item.Booking.SetNoShowHost(command.NoShow);
            bookingRepository.Update(item.Booking);
            subject = "host";
        }
        else
        {
            var attendee = await bookingAttendeeRepository.GetByIdAsync(command.AttendeeId, cancellationToken);
            if (attendee is null || attendee.BookingId != item.Booking.Id)
            {
                return Result.NotFound($"Attendee '{command.AttendeeId}' was not found.");
            }
            if (attendee.NoShow == command.NoShow)
            {
                return Result.Success();
            }
            attendee.MarkNoShow(command.NoShow);
            bookingAttendeeRepository.Update(attendee);
            subject = command.AttendeeId.Value;
        }

        var payload = JsonSerializer.Serialize(new { subject, noShow = command.NoShow });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.NoShow,
            now,
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        return Result.Success();
    }
}
