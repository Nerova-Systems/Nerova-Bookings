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
    string? PrivateLink = null
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
    }
}

public sealed class CreatePublicBookingHandler(
    PublicSchedulingResolver publicSchedulingResolver,
    IBookingRepository bookingRepository,
    PublicSlotCalculator publicSlotCalculator
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

        var endTime = command.StartTime.AddMinutes(command.Duration);
        var bookings = await bookingRepository.GetForOwnerRangeUnfilteredAsync(
            context.Profile.TenantId,
            context.Profile.OwnerUserId,
            command.StartTime.AddDays(-1),
            endTime.AddDays(1),
            cancellationToken
        );
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

        await bookingRepository.AddAsync(booking, cancellationToken);

        return new CreatePublicBookingResponse(booking.Id, booking.StartTime, booking.EndTime, status);
    }
}
