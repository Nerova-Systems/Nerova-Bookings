using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record CreatePublicBookingCommand(
    string Handle,
    string EventSlug,
    DateTimeOffset StartTime,
    int Duration,
    string TimeZone,
    string BookerName,
    string BookerEmail,
    Dictionary<string, string>? Responses = null,
    string? PrivateLink = null,
    BookingId? RescheduleBookingId = null,
    string? RescheduleReason = null,
    string? RescheduledBy = null
) : ICommand, IRequest<Result<CreatePublicBookingResponse>>;

public sealed class CreatePublicBookingValidator : AbstractValidator<CreatePublicBookingCommand>
{
    public CreatePublicBookingValidator()
    {
        RuleFor(command => command.Handle).NotEmpty().MaximumLength(80);
        RuleFor(command => command.EventSlug).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Duration).InclusiveBetween(5, 1440);
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(command => command.BookerName).NotEmpty().MaximumLength(120);
        RuleFor(command => command.BookerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.RescheduleReason).NotEmpty().MaximumLength(1000).When(command => command.RescheduleBookingId is not null);
        RuleFor(command => command.RescheduledBy).MaximumLength(320);
    }
}

public sealed class CreatePublicBookingHandler(
    PublicSchedulingResolver publicSchedulingResolver,
    IBookingRepository bookingRepository,
    IEventTypeRepository eventTypeRepository,
    PublicSlotCalculator publicSlotCalculator,
    TimeProvider timeProvider
) : IRequestHandler<CreatePublicBookingCommand, Result<CreatePublicBookingResponse>>
{
    public async Task<Result<CreatePublicBookingResponse>> Handle(CreatePublicBookingCommand command, CancellationToken cancellationToken)
    {
        var contextResult = await publicSchedulingResolver.ResolveAsync(command.Handle, command.EventSlug, command.PrivateLink, cancellationToken);
        if (!contextResult.IsSuccess)
        {
            return Result<CreatePublicBookingResponse>.From(contextResult);
        }

        var context = contextResult.Value!;
        if (!context.EventType.DurationOptions.Contains(command.Duration))
        {
            return Result<CreatePublicBookingResponse>.BadRequest("Duration is not available for this event type.");
        }

        Booking? originalBooking = null;
        if (command.RescheduleBookingId is not null)
        {
            var originalBookingResult = await ResolveOriginalRescheduleBookingAsync(command, context, cancellationToken);
            if (!originalBookingResult.IsSuccess)
            {
                return Result<CreatePublicBookingResponse>.From(originalBookingResult);
            }

            originalBooking = originalBookingResult.Value!;
        }

        var endTime = command.StartTime.AddMinutes(command.Duration);
        var bookings = await bookingRepository.GetForOwnerRangeUnfilteredAsync(
            context.Profile.TenantId,
            context.Profile.OwnerUserId,
            command.StartTime.AddDays(-1),
            endTime.AddDays(1),
            cancellationToken
        );
        if (originalBooking is not null)
        {
            bookings = bookings.Where(booking => booking.Id != originalBooking.Id).ToArray();
        }

        if (!publicSlotCalculator.IsSlotAvailable(context.EventType, context.Schedule, bookings, command.StartTime, command.Duration, command.TimeZone))
        {
            return Result<CreatePublicBookingResponse>.Conflict("The selected slot is no longer available.");
        }

        var allEventTypeBookings = await bookingRepository.GetForEventTypeUnfilteredAsync(context.Profile.TenantId, context.EventType.Id, cancellationToken);
        if (originalBooking is not null)
        {
            allEventTypeBookings = allEventTypeBookings.Where(booking => booking.Id != originalBooking.Id).ToArray();
        }

        var rulesResult = ValidateBookingRules(command, context.EventType, context.Schedule, allEventTypeBookings, timeProvider.GetUtcNow());
        if (!rulesResult.IsSuccess)
        {
            return Result<CreatePublicBookingResponse>.From(rulesResult);
        }

        var status = context.EventType.Settings.ConfirmationPolicy.RequiresConfirmation ? "pending" : "accepted";
        var booking = Booking.Create(
            context.Profile.TenantId,
            context.Profile.OwnerUserId,
            context.EventType.Id,
            command.StartTime,
            command.Duration,
            context.EventType.BeforeEventBufferMinutes,
            context.EventType.AfterEventBufferMinutes,
            context.EventType.Title,
            context.EventType.Description,
            context.EventType.LocationType,
            context.EventType.LocationValue,
            command.BookerName,
            command.BookerEmail,
            command.TimeZone,
            status,
            command.Responses ?? new Dictionary<string, string>(StringComparer.Ordinal)
        );

        if (originalBooking is not null)
        {
            originalBooking.RequestReschedule(command.RescheduleReason, command.RescheduledBy);
            booking.MarkAsReplacementFor(originalBooking.Id);
            bookingRepository.Update(originalBooking);
        }

        if (context.EventType.ConsumePrivateLink(command.PrivateLink))
        {
            eventTypeRepository.Update(context.EventType);
        }

        await bookingRepository.AddAsync(booking, cancellationToken);
        booking.RecordCreated();

        return new CreatePublicBookingResponse(booking.Id, booking.StartTime, booking.EndTime, status);
    }

    private static Result ValidateBookingRules(
        CreatePublicBookingCommand command,
        EventType eventType,
        Schedule schedule,
        Booking[] eventTypeBookings,
        DateTimeOffset now
    )
    {
        var activeBookings = eventTypeBookings
            .Where(booking => IsActiveBooking(booking, now))
            .ToArray();
        var activeBookerLimit = eventType.Settings.Limits.MaxActiveBookingsPerBooker;
        if (activeBookerLimit is > 0)
        {
            var normalizedBookerEmail = command.BookerEmail.Trim().ToLowerInvariant();
            var activeBookerBookings = activeBookings.Count(booking =>
                string.Equals(booking.BookerEmail, normalizedBookerEmail, StringComparison.OrdinalIgnoreCase) ||
                booking.Attendees.Any(attendee => string.Equals(attendee.Email, normalizedBookerEmail, StringComparison.OrdinalIgnoreCase))
            );
            if (activeBookerBookings >= activeBookerLimit)
            {
                return Result.BadRequest("You already have the maximum number of active bookings for this event type.");
            }
        }

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(command.StartTime, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone)).DateTime);
        var bookingsOnSelectedDay = activeBookings
            .Where(booking => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(booking.StartTime, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone)).DateTime) == localDate)
            .ToArray();
        if (eventType.Settings.Limits.MaxBookingsPerDay is > 0 && bookingsOnSelectedDay.Length >= eventType.Settings.Limits.MaxBookingsPerDay)
        {
            return Result.BadRequest("This event type has reached its booking limit for the selected day.");
        }

        if (eventType.Settings.Limits.MaxBookingDurationMinutesPerDay is > 0)
        {
            var bookedMinutes = bookingsOnSelectedDay.Sum(booking => (int)(booking.EndTime - booking.StartTime).TotalMinutes);
            if (bookedMinutes + command.Duration > eventType.Settings.Limits.MaxBookingDurationMinutesPerDay)
            {
                return Result.BadRequest("This event type has reached its booking duration limit for the selected day.");
            }
        }

        foreach (var field in eventType.Settings.BookingFields)
        {
            string? value = null;
            command.Responses?.TryGetValue(field.Name, out value);
            if (field.Required && IsMissingRequiredField(field, value))
            {
                return Result.BadRequest($"{field.Label} is required.");
            }

            if (!string.IsNullOrWhiteSpace(value) && !IsValidFieldOption(field, value))
            {
                return Result.BadRequest($"{field.Label} is not a valid option.");
            }
        }

        return Result.Success();
    }

    private static bool IsActiveBooking(Booking booking, DateTimeOffset now)
    {
        return booking.StartTime >= now && booking.Status.Trim().Equals("accepted", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingRequiredField(EventTypeBookingField field, string? value)
    {
        if (field.Type.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            return !bool.TryParse(value, out var accepted) || !accepted;
        }

        return string.IsNullOrWhiteSpace(value);
    }

    private static bool IsValidFieldOption(EventTypeBookingField field, string value)
    {
        if (field.Options.Length == 0) return true;
        if (field.Type.Equals("select", StringComparison.OrdinalIgnoreCase) || field.Type.Equals("radio", StringComparison.OrdinalIgnoreCase))
        {
            return field.Options.Any(option => string.Equals(option.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (field.Type.Equals("checkbox", StringComparison.OrdinalIgnoreCase) || field.Type.Equals("multiselect", StringComparison.OrdinalIgnoreCase))
        {
            var selectedOptions = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return selectedOptions.Length > 0 &&
                   selectedOptions.All(selectedOption => field.Options.Any(option => string.Equals(option.Value, selectedOption, StringComparison.OrdinalIgnoreCase)));
        }

        return true;
    }

    private async Task<Result<Booking>> ResolveOriginalRescheduleBookingAsync(CreatePublicBookingCommand command, PublicSchedulingContext context, CancellationToken cancellationToken)
    {
        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(context.Profile.TenantId, context.Profile.OwnerUserId, command.RescheduleBookingId!, cancellationToken);
        if (item is null || item.EventType.Id != context.EventType.Id)
        {
            return Result<Booking>.NotFound($"Booking '{command.RescheduleBookingId}' was not found.");
        }

        var action = BookingActionAvailability.ResolveReschedule(item.Booking, item.EventType, timeProvider.GetUtcNow());
        if (!action.Enabled)
        {
            return Result<Booking>.BadRequest(action.DisabledReason ?? "This booking cannot be rescheduled.");
        }

        return item.Booking;
    }
}
