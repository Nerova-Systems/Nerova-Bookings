using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

[PublicAPI]
public sealed record CancelBookingCommand(BookingId Id) : ICommand, IRequest<Result>;

public sealed class CancelBookingHandler(IBookingRepository bookingRepository, IExecutionContext executionContext, TimeProvider timeProvider)
    : IRequestHandler<CancelBookingCommand, Result>
{
    public async Task<Result> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, command.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.Id}' was not found.");
        }

        var cancelAction = BookingActionAvailability.ResolveCancel(item.Booking, item.EventType, timeProvider.GetUtcNow());
        if (!cancelAction.Enabled)
        {
            return Result.BadRequest(cancelAction.DisabledReason!);
        }

        item.Booking.Cancel();
        bookingRepository.Update(item.Booking);

        return Result.Success();
    }
}
