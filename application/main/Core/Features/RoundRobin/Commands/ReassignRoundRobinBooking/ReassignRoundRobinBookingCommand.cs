using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.RoundRobin.Commands.ReassignRoundRobinBooking;

[PublicAPI]
public sealed record ReassignRoundRobinBookingCommand(BookingId BookingId, UserId NewOwnerUserId)
    : ICommand, IRequest<Result>;

public sealed class ReassignRoundRobinBookingValidator : AbstractValidator<ReassignRoundRobinBookingCommand>
{
    public ReassignRoundRobinBookingValidator()
    {
        RuleFor(command => command.BookingId.Value).NotEmpty().WithMessage("Booking ID is required.");
        RuleFor(command => command.NewOwnerUserId.Value).NotEmpty().WithMessage("New owner user ID is required.");
    }
}

public sealed class ReassignRoundRobinBookingHandler(
    IBookingRepository bookingRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<ReassignRoundRobinBookingCommand, Result>
{
    public async Task<Result> Handle(ReassignRoundRobinBookingCommand command, CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!RoundRobinAuthorization.HasRoundRobinFeature(userInfo))
        {
            return Result.Forbidden(RoundRobinAuthorization.RoundRobinFeatureDisabledMessage);
        }

        if (!RoundRobinAuthorization.CanManageRoundRobinHosts(userInfo))
        {
            return Result.Forbidden(RoundRobinAuthorization.ManageRoundRobinHostsForbiddenMessage);
        }

        var tenantId = userInfo.TenantId;
        var teamId = executionContext.ActiveTeamId;
        if (tenantId is null || teamId is null)
        {
            return Result.BadRequest("A team context is required to reassign round-robin bookings.");
        }

        // ownerUserId is not used in the team-scoped code path of GetForOwnerWithEventTypeAsync
        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, userInfo.Id!, teamId, command.BookingId, cancellationToken);
        if (item is null)
        {
            return Result.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        if (item.EventType.SchedulingType != SchedulingType.RoundRobin)
        {
            return Result.BadRequest("Only round-robin bookings can be reassigned.");
        }

        item.Booking.Reassign(command.NewOwnerUserId);
        bookingRepository.Update(item.Booking);
        events.CollectEvent(new RoundRobinBookingReassigned(command.BookingId, command.NewOwnerUserId));

        return Result.Success();
    }
}
