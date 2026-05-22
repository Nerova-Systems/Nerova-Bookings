using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record EditBookingLocationCommand(BookingId Id, string? LocationType, string? LocationValue) : ICommand, IRequest<Result>;

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
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<EditBookingLocationCommand, Result>
{
    public async Task<Result> Handle(EditBookingLocationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        if (item.Booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Result.BadRequest("Closed bookings cannot have their location edited.");
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

        return Result.Success();
    }
}
