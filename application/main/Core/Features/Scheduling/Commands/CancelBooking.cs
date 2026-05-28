using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.Scheduling.Shared;
using Main.Features.Webhooks.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Cancel)]
public sealed record CancelBookingCommand(BookingId Id, string? Reason = null) : ICommand, IRequest<Result>;

public sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator()
    {
        RuleFor(command => command.Reason).MaximumLength(1000);
    }
}

public sealed class CancelBookingHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    IBookingWebhookNotifier webhookNotifier,
    IBookingNotificationDispatcher bookingNotificationDispatcher,
    TimeProvider timeProvider
) : IRequestHandler<CancelBookingCommand, Result>
{
    public async Task<Result> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        // Admin/Owner may cancel any booking in the tenant; Member is restricted to bookings they
        // host (owner-scoped lookup). Member calls on a booking they don't own return NotFound to
        // avoid leaking existence — diverges from the original task spec which suggested 403.
        var isAdminOrOwner = executionContext.UserInfo.Role is SystemRoles.Owner or SystemRoles.Admin;
        var item = isAdminOrOwner
            ? await bookingRepository.GetByIdInTenantWithEventTypeAsync(tenantId, command.Id, cancellationToken)
            : await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        var cancelAction = BookingActionAvailability.ResolveCancel(item.Booking, item.EventType, timeProvider.GetUtcNow());
        if (!cancelAction.Enabled)
        {
            return Result.BadRequest(cancelAction.DisabledReason!);
        }

        if (item.EventType.Settings.ConfirmationPolicy.RequiresCancellationReason && string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result.BadRequest("A cancellation reason is required for this event type.");
        }

        item.Booking.Cancel(command.Reason, ownerUserId.Value);
        bookingRepository.Update(item.Booking);

        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Cancelled,
            timeProvider.GetUtcNow(),
            ownerUserId
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        // Fan out a BookingCancelled webhook. Best-effort — failures are swallowed inside the
        // notifier so a flaky subscriber endpoint cannot fail the cancellation.
        await webhookNotifier.NotifyAsync(
            WebhookEventType.BookingCancelled,
            item.Booking,
            item.EventType,
            null,
            null,
            cancellationToken
        );

        await bookingNotificationDispatcher.DispatchAsync(item.Booking, item.EventType, BookingNotificationKind.Cancelled, cancellationToken);

        return Result.Success();
    }
}
