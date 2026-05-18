using FluentValidation;
using JetBrains.Annotations;
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

        var requiredMissingField = context.EventType.Settings.BookingFields
            .Where(field => field.Required)
            .FirstOrDefault(field => command.Responses?.ContainsKey(field.Name) != true || string.IsNullOrWhiteSpace(command.Responses[field.Name]));
        if (requiredMissingField is not null)
        {
            return Result<CreatePublicBookingResponse>.BadRequest($"{requiredMissingField.Label} is required.");
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

        await bookingRepository.AddAsync(booking, cancellationToken);

        return new CreatePublicBookingResponse(booking.Id, booking.StartTime, booking.EndTime, status);
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
