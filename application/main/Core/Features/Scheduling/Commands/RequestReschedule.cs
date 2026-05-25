using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.Webhooks.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Host-initiated reschedule request: invalidates the existing booking time so the booker can
///     pick a new slot via the public scheduling page. Mirrors cal.com <c>requestReschedule</c>.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Reschedule)]
public sealed record RequestRescheduleCommand(BookingId Id, string? Reason) : ICommand, IRequest<Result>;

public sealed class RequestRescheduleValidator : AbstractValidator<RequestRescheduleCommand>
{
    public RequestRescheduleValidator()
    {
        RuleFor(command => command.Reason).MaximumLength(1000);
    }
}

public sealed class RequestRescheduleHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    IBookingWebhookNotifier webhookNotifier,
    TimeProvider timeProvider
) : IRequestHandler<RequestRescheduleCommand, Result>
{
    public async Task<Result> Handle(RequestRescheduleCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var isAdminOrOwner = executionContext.UserInfo.Role is SystemRoles.Owner or SystemRoles.Admin;
        var item = isAdminOrOwner
            ? await bookingRepository.GetByIdInTenantWithEventTypeAsync(tenantId, command.Id, cancellationToken)
            : await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result.BadRequest("This booking cannot be rescheduled.");
        }

        if (item.Booking.EndTime <= timeProvider.GetUtcNow())
        {
            return Result.BadRequest("Past bookings cannot be rescheduled.");
        }

        item.Booking.MarkRescheduled(ownerUserId.Value);
        bookingRepository.Update(item.Booking);

        var payload = JsonSerializer.Serialize(new { reason = command.Reason });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Rescheduled,
            timeProvider.GetUtcNow(),
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        // Fan out a BookingRescheduled webhook. Note: the host has only marked the booking as
        // rescheduled — the booker still needs to pick a new slot via the public scheduling
        // page. Cal.com fires `BOOKING_RESCHEDULED` at this point too, so subscribers stay in
        // sync with cal.com integrations.
        await webhookNotifier.NotifyAsync(
            WebhookEventType.BookingRescheduled,
            item.Booking,
            item.EventType,
            attendees: null,
            report: null,
            cancellationToken
        );

        return Result.Success();
    }
}
