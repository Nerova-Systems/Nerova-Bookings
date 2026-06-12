using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.Scheduling.Shared;
using Main.Features.Webhooks.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Customer-initiated cancellation over WhatsApp. The customer's identity is their verified phone
///     number from the conversation state: the booking must belong to the tenant AND carry the same
///     booker phone, so the worst case is a customer managing their own bookings. Applies the same
///     cancellation-policy checks as the staff-side command and fans out the same side effects.
/// </summary>
[PublicAPI]
public sealed record CancelCustomerBookingCommand(TenantId TenantId, string CustomerPhoneNumber, BookingId BookingId, string? Reason = null)
    : ICommand, IRequest<Result>;

public sealed class CancelCustomerBookingValidator : AbstractValidator<CancelCustomerBookingCommand>
{
    public CancelCustomerBookingValidator()
    {
        RuleFor(command => command.Reason).MaximumLength(1000).WithMessage("Reason must be at most 1000 characters.");
    }
}

public sealed class CancelCustomerBookingHandler(
    IBookingRepository bookingRepository,
    IEventTypeRepository eventTypeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IBookingWebhookNotifier webhookNotifier,
    IBookingNotificationDispatcher bookingNotificationDispatcher,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<CancelCustomerBookingCommand, Result>
{
    public async Task<Result> Handle(CancelCustomerBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdUnfilteredAsync(command.BookingId, cancellationToken);
        if (booking is null || booking.TenantId != command.TenantId || booking.BookerPhone != command.CustomerPhoneNumber)
        {
            return Result.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        var eventType = await eventTypeRepository.GetByIdUnfilteredAsync(command.TenantId, booking.EventTypeId, cancellationToken);
        if (eventType is null)
        {
            return Result.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        var cancelAction = BookingActionAvailability.ResolveCancel(booking, eventType, timeProvider.GetUtcNow());
        if (!cancelAction.Enabled)
        {
            return Result.BadRequest(cancelAction.DisabledReason!);
        }

        booking.Cancel(command.Reason, "customer-whatsapp");
        bookingRepository.Update(booking);

        var entry = BookingHistoryEntry.Create(command.TenantId, booking.Id, BookingHistoryEventType.Cancelled, timeProvider.GetUtcNow());
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        await webhookNotifier.NotifyAsync(WebhookEventType.BookingCancelled, booking, eventType, null, null, cancellationToken);
        await bookingNotificationDispatcher.DispatchAsync(booking, eventType, BookingNotificationKind.Cancelled, cancellationToken);

        events.CollectEvent(new BookingCancelledByCustomer(booking.Id));

        return Result.Success();
    }
}
