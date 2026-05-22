using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record ReassignBookingCommand(BookingId Id, UserId NewOwnerUserId, string? Reason) : ICommand, IRequest<Result>;

public sealed class ReassignBookingValidator : AbstractValidator<ReassignBookingCommand>
{
    public ReassignBookingValidator()
    {
        RuleFor(command => command.NewOwnerUserId.Value).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(1000);
    }
}

public sealed class ReassignBookingHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<ReassignBookingCommand, Result>
{
    public async Task<Result> Handle(ReassignBookingCommand command, CancellationToken cancellationToken)
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
            return Result.BadRequest("Closed bookings cannot be reassigned.");
        }

        var previousOwnerId = item.Booking.OwnerUserId;
        item.Booking.Reassign(command.NewOwnerUserId, command.Reason, ownerUserId);
        bookingRepository.Update(item.Booking);

        var payload = JsonSerializer.Serialize(new { previousOwnerId = previousOwnerId.Value, newOwnerId = command.NewOwnerUserId.Value, reason = command.Reason });
        var entry = BookingHistoryEntry.Create(
            tenantId,
            item.Booking.Id,
            BookingHistoryEventType.Reassigned,
            timeProvider.GetUtcNow(),
            ownerUserId,
            payload
        );
        await bookingHistoryEntryRepository.AddAsync(entry, cancellationToken);

        return Result.Success();
    }
}
