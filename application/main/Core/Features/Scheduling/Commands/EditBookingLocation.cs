using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Queries;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Update)]
public sealed record EditBookingLocationCommand(BookingId Id, string? LocationType, string? LocationValue) : ICommand, IRequest<Result<BookingLifecycleResponse>>;

public sealed class EditBookingLocationValidator : AbstractValidator<EditBookingLocationCommand>
{
    public EditBookingLocationValidator()
    {
        RuleFor(command => command.LocationType).MaximumLength(80);
        RuleFor(command => command.LocationValue).MaximumLength(2000);
    }
}

public sealed class EditBookingLocationHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<EditBookingLocationCommand, Result<BookingLifecycleResponse>>
{
    public async Task<Result<BookingLifecycleResponse>> Handle(EditBookingLocationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingLifecycleResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingLifecycleResponse>.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result<BookingLifecycleResponse>.BadRequest("Closed bookings cannot have their location edited.");
        }

        item.Booking.SetLocation(command.LocationType, command.LocationValue);
        bookingRepository.Update(item.Booking);

        var payload = JsonSerializer.Serialize(new { locationType = command.LocationType, locationValue = command.LocationValue });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.LocationChanged,
            timeProvider.GetUtcNow(),
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        var attendees = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var attendeeResponses = attendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray();
        return new BookingLifecycleResponse(item.Booking.Id, item.Booking.Status, attendeeResponses, item.Booking.LocationType, item.Booking.LocationValue);
    }
}
