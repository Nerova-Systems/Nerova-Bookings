using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

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
    IHostRepository hostRepository,
    PublicSlotCalculator publicSlotCalculator,
    CollectiveSlotCalculator collectiveSlotCalculator,
    RoundRobinSlotCalculator roundRobinSlotCalculator
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

        bool slotAvailable;
        UserId ownerUserId = context.Profile.OwnerUserId;

        if (context.EventType.SchedulingType == SchedulingType.Collective)
        {
            var hosts = await hostRepository.GetForEventTypeUnfilteredAsync(context.EventType.Id, cancellationToken);
            var hostUserIds = hosts.Select(h => h.UserId).ToList();
            var hostBookings = hostUserIds.Count > 0
                ? await bookingRepository.GetForMultipleOwnersRangeAsync(
                    context.Profile.TenantId,
                    hostUserIds,
                    command.StartTime.AddDays(-1),
                    endTime.AddDays(1),
                    cancellationToken)
                : (IReadOnlyDictionary<UserId, Booking[]>)new Dictionary<UserId, Booking[]>();

            slotAvailable = collectiveSlotCalculator.IsSlotAvailable(context.EventType, context.Schedule, hostBookings, command.StartTime, command.Duration, command.TimeZone);
        }
        else if (context.EventType.SchedulingType == SchedulingType.RoundRobin)
        {
            var hosts = await hostRepository.GetForEventTypeUnfilteredAsync(context.EventType.Id, cancellationToken);
            var hostUserIds = hosts.Select(h => h.UserId).ToList();
            var hostBookings = hostUserIds.Count > 0
                ? await bookingRepository.GetForMultipleOwnersRangeAsync(
                    context.Profile.TenantId,
                    hostUserIds,
                    command.StartTime.AddDays(-1),
                    endTime.AddDays(1),
                    cancellationToken)
                : (IReadOnlyDictionary<UserId, Booking[]>)new Dictionary<UserId, Booking[]>();

            slotAvailable = roundRobinSlotCalculator.IsSlotAvailable(context.EventType, context.Schedule, hostBookings, hosts, command.StartTime, command.Duration, command.TimeZone);

            if (slotAvailable)
            {
                var selectedHost = roundRobinSlotCalculator.SelectRoundRobinHost(
                    hosts,
                    hostBookings,
                    command.StartTime,
                    command.Duration,
                    context.EventType.BeforeEventBufferMinutes,
                    context.EventType.AfterEventBufferMinutes);

                if (selectedHost is not null)
                {
                    ownerUserId = selectedHost;
                }
            }
        }
        else
        {
            var bookings = await bookingRepository.GetForOwnerRangeUnfilteredAsync(
                context.Profile.TenantId,
                context.Profile.OwnerUserId,
                command.StartTime.AddDays(-1),
                endTime.AddDays(1),
                cancellationToken
            );
            slotAvailable = publicSlotCalculator.IsSlotAvailable(context.EventType, context.Schedule, bookings, command.StartTime, command.Duration, command.TimeZone);
        }

        if (!slotAvailable)
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

        var status = context.EventType.Settings.ConfirmationPolicy.RequiresConfirmation ? BookingStatus.Pending : BookingStatus.Accepted;
        var booking = Booking.Create(
            context.Profile.TenantId,
            ownerUserId,
            context.EventType.Id,
            command.StartTime,
            command.Duration,
            context.EventType.BeforeEventBufferMinutes,
            context.EventType.AfterEventBufferMinutes,
            command.BookerName,
            command.BookerEmail,
            command.TimeZone,
            status,
            command.Responses ?? new Dictionary<string, string>(StringComparer.Ordinal),
            context.EventType.TeamId
        );

        await bookingRepository.AddAsync(booking, cancellationToken);

        return new CreatePublicBookingResponse(booking.Id, booking.StartTime, booking.EndTime, status.ToString());
    }
}
