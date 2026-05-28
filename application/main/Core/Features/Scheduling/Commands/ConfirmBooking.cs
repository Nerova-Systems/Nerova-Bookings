using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.Scheduling.Queries;
using Main.Features.Scheduling.Shared;
using Main.Features.Webhooks.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record ConfirmBookingCommand(BookingId Id) : ICommand, IRequest<Result<BookingLifecycleResponse>>;

public sealed class ConfirmBookingHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IBookingWebhookNotifier webhookNotifier,
    IBookingNotificationDispatcher bookingNotificationDispatcher
) : IRequestHandler<ConfirmBookingCommand, Result<BookingLifecycleResponse>>
{
    public async Task<Result<BookingLifecycleResponse>> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingLifecycleResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingLifecycleResponse>.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status != BookingStatus.Pending && item.Booking.Status != BookingStatus.AwaitingHost)
        {
            return Result<BookingLifecycleResponse>.BadRequest($"Booking '{command.Id}' is not awaiting confirmation.");
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

        // Host accepted a pending booking — mirrors cal.com which fires BOOKING_CREATED on host
        // confirmation since the booking only becomes "real" at this point. Best-effort: failures
        // are logged inside the notifier, never bubble up.
        await webhookNotifier.NotifyAsync(
            WebhookEventType.BookingCreated,
            item.Booking,
            item.EventType,
            null,
            null,
            cancellationToken
        );

        // Confirmed booking transitions Pending → Accepted. Send the host-confirmed email to let the
        // booker know their slot has been accepted. Mirrors cal.com's host-confirmed notification.
        await bookingNotificationDispatcher.DispatchAsync(item.Booking, item.EventType, BookingNotificationKind.Created, cancellationToken);

        var attendees = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var attendeeResponses = attendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray();
        return new BookingLifecycleResponse(item.Booking.Id, item.Booking.Status, attendeeResponses, item.Booking.LocationType, item.Booking.LocationValue);
    }
}
