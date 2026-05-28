using System.Text.Json;
using FluentValidation;
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

/// <summary>
///     Host-initiated reschedule request: invalidates the existing booking time so the booker can
///     pick a new slot via the public scheduling page. Mirrors cal.com <c>requestReschedule</c>.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Reschedule)]
public sealed record RequestRescheduleCommand(BookingId Id, string? RescheduleReason) : ICommand, IRequest<Result<BookingLifecycleResponse>>;

public sealed class RequestRescheduleValidator : AbstractValidator<RequestRescheduleCommand>
{
    public RequestRescheduleValidator()
    {
        RuleFor(command => command.RescheduleReason).MaximumLength(1000);
    }
}

public sealed class RequestRescheduleHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    IBookingWebhookNotifier webhookNotifier,
    TimeProvider timeProvider
) : IRequestHandler<RequestRescheduleCommand, Result<BookingLifecycleResponse>>
{
    public async Task<Result<BookingLifecycleResponse>> Handle(RequestRescheduleCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingLifecycleResponse>.Unauthorized("Authentication is required.");
        }

        var isAdminOrOwner = executionContext.UserInfo.Role is SystemRoles.Owner or SystemRoles.Admin;
        var item = isAdminOrOwner
            ? await bookingRepository.GetByIdInTenantWithEventTypeAsync(tenantId, command.Id, cancellationToken)
            : await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingLifecycleResponse>.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result<BookingLifecycleResponse>.BadRequest("This booking cannot be rescheduled.");
        }

        if (item.Booking.EndTime <= timeProvider.GetUtcNow())
        {
            return Result<BookingLifecycleResponse>.BadRequest("Past bookings cannot be rescheduled.");
        }

        item.Booking.RequestReschedule(command.RescheduleReason, executionContext.UserInfo.Email);
        bookingRepository.Update(item.Booking);

        var payload = JsonSerializer.Serialize(new { reason = command.RescheduleReason });
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
            null,
            null,
            cancellationToken
        );

        var attendees = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var attendeeResponses = attendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray();
        return new BookingLifecycleResponse(item.Booking.Id, item.Booking.Status, attendeeResponses, item.Booking.LocationType, item.Booking.LocationValue);
    }
}
