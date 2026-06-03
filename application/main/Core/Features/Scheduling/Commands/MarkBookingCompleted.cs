using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Marks a booking as <see cref="BookingStatus.Completed" /> (the session has occurred).
///     Raises <c>BookingCompletedEvent</c> which the after-session payment dispatcher
///     reacts to for tenants whose <c>PaymentTiming = AfterSession</c>.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record MarkBookingCompletedCommand(BookingId Id) : ICommand, IRequest<Result>;

public sealed class MarkBookingCompletedHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IPublisher publisher
) : IRequestHandler<MarkBookingCompletedCommand, Result>
{
    public async Task<Result> Handle(MarkBookingCompletedCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        // Mirror CancelBooking authorisation: Admin/Owner may complete any booking in the tenant;
        // Member is restricted to bookings they host. Returning NotFound for unauthorised access
        // avoids leaking booking existence to non-hosts.
        var isAdminOrOwner = executionContext.UserInfo.Role is SystemRoles.Owner or SystemRoles.Admin;
        var item = isAdminOrOwner
            ? await bookingRepository.GetByIdInTenantWithEventTypeAsync(tenantId, command.Id, cancellationToken)
            : await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status == BookingStatus.Completed)
        {
            return Result.BadRequest($"Booking '{command.Id}' is already completed.");
        }

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result.BadRequest($"Booking '{command.Id}' cannot be completed because it is {item.Booking.Status}.");
        }

        item.Booking.MarkCompleted();
        bookingRepository.Update(item.Booking);

        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Completed,
            timeProvider.GetUtcNow(),
            ownerUserId
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        // Fan out to the post-session payment dispatcher (best-effort; mirrors how ConfirmBooking
        // dispatches webhook + email notifications synchronously inside the handler).
        await publisher.Publish(new BookingCompletedNotification(item.Booking.Id, tenantId), cancellationToken);

        return Result.Success();
    }
}
