using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record ConfirmBookingCommand(BookingId Id) : ICommand, IRequest<Result>;

public sealed class ConfirmBookingHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IBookingNotificationDispatcher bookingNotificationDispatcher
) : IRequestHandler<ConfirmBookingCommand, Result>
{
    public async Task<Result> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
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

        if (item.Booking.Status != BookingStatus.Pending && item.Booking.Status != BookingStatus.AwaitingHost)
        {
            return Result.BadRequest($"Booking '{command.Id}' is not awaiting confirmation.");
        }

        item.Booking.Confirm();
        bookingRepository.Update(item.Booking);

        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Confirmed,
            timeProvider.GetUtcNow(),
            ownerUserId
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        // Confirmed booking transitions Pending → Accepted. Send the host-confirmed email to let the
        // booker know their slot has been accepted. Mirrors cal.com's host-confirmed notification.
        await bookingNotificationDispatcher.DispatchAsync(item.Booking, item.EventType, BookingNotificationKind.Created, cancellationToken);

        return Result.Success();
    }
}
