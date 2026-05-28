using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Schedules.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.EventTypes.Queries;

/// <summary>
///     Returns each host of the event type with the availability windows they publish
///     through their assigned schedule. The (from, to) range is currently echoed back
///     for the frontend to compute per-day overlays — date-override expansion within
///     the range is deferred until cross-SCS travel/OOO data is exposed.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
public sealed record GetHostsForAvailabilityQuery(EventTypeId EventTypeId, DateOnly From, DateOnly To)
    : IRequest<Result<HostsForAvailabilityResponse>>;

public sealed class GetHostsForAvailabilityValidator : AbstractValidator<GetHostsForAvailabilityQuery>
{
    public GetHostsForAvailabilityValidator()
    {
        RuleFor(query => query)
            .Must(query => query.From <= query.To)
            .WithMessage("From must be on or before To.");
    }
}

public sealed class GetHostsForAvailabilityHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetHostsForAvailabilityQuery, Result<HostsForAvailabilityResponse>>
{
    public async Task<Result<HostsForAvailabilityResponse>> Handle(GetHostsForAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var callerUserId = executionContext.UserInfo.Id;
        if (callerUserId is null)
        {
            return Result<HostsForAvailabilityResponse>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(query.EventTypeId, cancellationToken);
        if (eventType is null || (eventType.TeamId is null && eventType.OwnerUserId != callerUserId))
        {
            return Result<HostsForAvailabilityResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        var hosts = await hostRepository.GetForEventTypeAsync(query.EventTypeId, cancellationToken);
        var schedule = await scheduleRepository.GetByIdAsync(eventType.ScheduleId, cancellationToken);

        var availability = new List<HostAvailabilityResponse>();
        var fallbackUserIds = hosts.Length == 0 ? [eventType.OwnerUserId] : hosts.Select(host => host.UserId).Distinct().ToArray();

        foreach (var userId in fallbackUserIds)
        {
            var windows = schedule?.AvailabilityWindows
                .Select(window => new HostAvailabilityWindowResponse(window.Days, window.StartMinute, window.EndMinute))
                .ToArray() ?? [];
            availability.Add(new HostAvailabilityResponse(userId, schedule?.TimeZone ?? string.Empty, windows));
        }

        return new HostsForAvailabilityResponse(availability.ToArray());
    }
}
