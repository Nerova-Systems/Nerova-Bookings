using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Customer-initiated reschedule over WhatsApp: verifies the booking belongs to the customer's
///     verified phone number, checks the event type's reschedule policy, then composes the existing
///     public booking command with <c>RescheduleBookingId</c> — the deterministic core cancels the old
///     booking and creates the new one with full validation (availability, limits, buffers).
/// </summary>
[PublicAPI]
public sealed record RescheduleCustomerBookingCommand(TenantId TenantId, string CustomerPhoneNumber, BookingId BookingId, DateTimeOffset NewStartTime, string? Reason = null)
    : ICommand, IRequest<Result<CreatePublicBookingResponse>>;

public sealed class RescheduleCustomerBookingValidator : AbstractValidator<RescheduleCustomerBookingCommand>
{
    public RescheduleCustomerBookingValidator()
    {
        RuleFor(command => command.Reason).MaximumLength(1000).WithMessage("Reason must be at most 1000 characters.");
    }
}

public sealed class RescheduleCustomerBookingHandler(
    IBookingRepository bookingRepository,
    IEventTypeRepository eventTypeRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IMediator mediator,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<RescheduleCustomerBookingCommand, Result<CreatePublicBookingResponse>>
{
    public async Task<Result<CreatePublicBookingResponse>> Handle(RescheduleCustomerBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdUnfilteredAsync(command.BookingId, cancellationToken);
        if (booking is null || booking.TenantId != command.TenantId || booking.BookerPhone != command.CustomerPhoneNumber)
        {
            return Result<CreatePublicBookingResponse>.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        var eventType = await eventTypeRepository.GetByIdUnfilteredAsync(command.TenantId, booking.EventTypeId, cancellationToken);
        if (eventType is null)
        {
            return Result<CreatePublicBookingResponse>.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        var rescheduleAction = BookingActionAvailability.ResolveReschedule(booking, eventType, timeProvider.GetUtcNow());
        if (!rescheduleAction.Enabled)
        {
            return Result<CreatePublicBookingResponse>.BadRequest(rescheduleAction.DisabledReason!);
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (profile is null)
        {
            return Result<CreatePublicBookingResponse>.BadRequest("Online booking is not available for this business.");
        }

        var durationMinutes = (int)(booking.EndTime - booking.StartTime).TotalMinutes;
        var createCommand = new CreatePublicBookingCommand(
            profile.Handle,
            eventType.Slug,
            command.NewStartTime,
            durationMinutes,
            booking.TimeZone,
            booking.BookerName,
            booking.BookerEmail,
            RescheduleBookingId: booking.Id,
            RescheduleReason: command.Reason,
            RescheduledBy: "customer-whatsapp",
            BookerPhone: command.CustomerPhoneNumber
        );

        var result = await mediator.Send(createCommand, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return result;
        }

        events.CollectEvent(new BookingRescheduledByCustomer(booking.Id, result.Value.Id));

        return result;
    }
}
