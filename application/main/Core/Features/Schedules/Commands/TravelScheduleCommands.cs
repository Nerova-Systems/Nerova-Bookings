using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Schedules.Commands;

// ─── List ────────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Read)]
public sealed record GetTravelSchedulesQuery(UserId UserId) : IRequest<Result<TravelSchedulesResponse>>;

public sealed class GetTravelSchedulesHandler(
    ITravelScheduleRepository travelScheduleRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetTravelSchedulesQuery, Result<TravelSchedulesResponse>>
{
    public async Task<Result<TravelSchedulesResponse>> Handle(GetTravelSchedulesQuery query, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        if (callerId is null) return Result<TravelSchedulesResponse>.Unauthorized("Authentication is required.");
        if (callerId != query.UserId) return Result<TravelSchedulesResponse>.Forbidden("Travel schedules can only be managed by their owner.");

        var travelSchedules = await travelScheduleRepository.GetForUserAsync(query.UserId, cancellationToken);
        return new TravelSchedulesResponse(travelSchedules.Select(TravelScheduleResponse.From).ToArray());
    }
}

// ─── Create ──────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Create)]
public sealed record CreateTravelScheduleCommand(
    UserId UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    ScheduleId? ScheduleId
) : ICommand, IRequest<Result<TravelScheduleResponse>>;

public sealed class CreateTravelScheduleValidator : AbstractValidator<CreateTravelScheduleCommand>
{
    public CreateTravelScheduleValidator()
    {
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(command => command).Must(command => command.EndDate >= command.StartDate)
            .WithMessage("Travel schedule end date must be on or after start date.")
            .WithName("endDate");
    }
}

public sealed class CreateTravelScheduleHandler(
    ITravelScheduleRepository travelScheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateTravelScheduleCommand, Result<TravelScheduleResponse>>
{
    public async Task<Result<TravelScheduleResponse>> Handle(CreateTravelScheduleCommand command, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        var tenantId = executionContext.TenantId;
        if (callerId is null || tenantId is null) return Result<TravelScheduleResponse>.Unauthorized("Authentication is required.");
        if (callerId != command.UserId) return Result<TravelScheduleResponse>.Forbidden("Travel schedules can only be managed by their owner.");

        TravelSchedule travel;
        try
        {
            travel = TravelSchedule.Create(tenantId, command.UserId, command.StartDate, command.EndDate, command.TimeZone, command.ScheduleId);
        }
        catch (ArgumentException exception)
        {
            return Result<TravelScheduleResponse>.BadRequest(exception.Message);
        }

        await travelScheduleRepository.AddAsync(travel, cancellationToken);
        events.CollectEvent(new TravelScheduleCreated(travel.Id));

        return TravelScheduleResponse.From(travel);
    }
}

// ─── Update ──────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Update)]
public sealed record UpdateTravelScheduleCommand(
    UserId UserId,
    TravelScheduleId Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    ScheduleId? ScheduleId
) : ICommand, IRequest<Result<TravelScheduleResponse>>;

public sealed class UpdateTravelScheduleValidator : AbstractValidator<UpdateTravelScheduleCommand>
{
    public UpdateTravelScheduleValidator()
    {
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(command => command).Must(command => command.EndDate >= command.StartDate)
            .WithMessage("Travel schedule end date must be on or after start date.")
            .WithName("endDate");
    }
}

public sealed class UpdateTravelScheduleHandler(
    ITravelScheduleRepository travelScheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateTravelScheduleCommand, Result<TravelScheduleResponse>>
{
    public async Task<Result<TravelScheduleResponse>> Handle(UpdateTravelScheduleCommand command, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        if (callerId is null) return Result<TravelScheduleResponse>.Unauthorized("Authentication is required.");
        if (callerId != command.UserId) return Result<TravelScheduleResponse>.Forbidden("Travel schedules can only be managed by their owner.");

        var travel = await travelScheduleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (travel is null || travel.UserId != command.UserId)
        {
            return Result<TravelScheduleResponse>.NotFound($"Travel schedule '{command.Id}' was not found.");
        }

        try
        {
            travel.Update(command.StartDate, command.EndDate, command.TimeZone, command.ScheduleId);
        }
        catch (ArgumentException exception)
        {
            return Result<TravelScheduleResponse>.BadRequest(exception.Message);
        }

        travelScheduleRepository.Update(travel);
        events.CollectEvent(new TravelScheduleUpdated(travel.Id));

        return TravelScheduleResponse.From(travel);
    }
}

// ─── Delete ──────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Delete)]
public sealed record DeleteTravelScheduleCommand(UserId UserId, TravelScheduleId Id) : ICommand, IRequest<Result>;

public sealed class DeleteTravelScheduleHandler(
    ITravelScheduleRepository travelScheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteTravelScheduleCommand, Result>
{
    public async Task<Result> Handle(DeleteTravelScheduleCommand command, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        if (callerId is null) return Result.Unauthorized("Authentication is required.");
        if (callerId != command.UserId) return Result.Forbidden("Travel schedules can only be managed by their owner.");

        var travel = await travelScheduleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (travel is null || travel.UserId != command.UserId)
        {
            return Result.NotFound($"Travel schedule '{command.Id}' was not found.");
        }

        travelScheduleRepository.Remove(travel);
        events.CollectEvent(new TravelScheduleDeleted(travel.Id));

        return Result.Success();
    }
}
