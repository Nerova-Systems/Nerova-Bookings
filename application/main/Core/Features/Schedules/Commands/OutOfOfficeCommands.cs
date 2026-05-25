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
public sealed record GetOutOfOfficesQuery(UserId UserId) : IRequest<Result<OutOfOfficesResponse>>;

public sealed class GetOutOfOfficesHandler(
    IOutOfOfficeRepository outOfOfficeRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetOutOfOfficesQuery, Result<OutOfOfficesResponse>>
{
    public async Task<Result<OutOfOfficesResponse>> Handle(GetOutOfOfficesQuery query, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        if (callerId is null) return Result<OutOfOfficesResponse>.Unauthorized("Authentication is required.");
        if (callerId != query.UserId) return Result<OutOfOfficesResponse>.Forbidden("Out-of-office entries can only be managed by their owner.");

        var entries = await outOfOfficeRepository.GetForUserAsync(query.UserId, cancellationToken);
        return new OutOfOfficesResponse(entries.Select(OutOfOfficeResponse.From).ToArray());
    }
}

// ─── Create ──────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Create)]
public sealed record CreateOutOfOfficeCommand(
    UserId UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    UserId? ToUserId,
    string? Reason,
    string? Notes
) : ICommand, IRequest<Result<OutOfOfficeResponse>>;

public sealed class CreateOutOfOfficeValidator : AbstractValidator<CreateOutOfOfficeCommand>
{
    public CreateOutOfOfficeValidator()
    {
        RuleFor(command => command.Reason).MaximumLength(120);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command).Must(command => command.EndDate >= command.StartDate)
            .WithMessage("Out-of-office end date must be on or after start date.")
            .WithName("endDate");
    }
}

public sealed class CreateOutOfOfficeHandler(
    IOutOfOfficeRepository outOfOfficeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateOutOfOfficeCommand, Result<OutOfOfficeResponse>>
{
    public async Task<Result<OutOfOfficeResponse>> Handle(CreateOutOfOfficeCommand command, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        var tenantId = executionContext.TenantId;
        if (callerId is null || tenantId is null) return Result<OutOfOfficeResponse>.Unauthorized("Authentication is required.");
        if (callerId != command.UserId) return Result<OutOfOfficeResponse>.Forbidden("Out-of-office entries can only be managed by their owner.");

        OutOfOffice entry;
        try
        {
            entry = OutOfOffice.Create(tenantId, command.UserId, command.StartDate, command.EndDate, command.ToUserId, command.Reason, command.Notes);
        }
        catch (ArgumentException exception)
        {
            return Result<OutOfOfficeResponse>.BadRequest(exception.Message);
        }

        await outOfOfficeRepository.AddAsync(entry, cancellationToken);
        events.CollectEvent(new OutOfOfficeCreated(entry.Id));

        return OutOfOfficeResponse.From(entry);
    }
}

// ─── Update ──────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Update)]
public sealed record UpdateOutOfOfficeCommand(
    UserId UserId,
    OutOfOfficeId Id,
    DateOnly StartDate,
    DateOnly EndDate,
    UserId? ToUserId,
    string? Reason,
    string? Notes
) : ICommand, IRequest<Result<OutOfOfficeResponse>>;

public sealed class UpdateOutOfOfficeValidator : AbstractValidator<UpdateOutOfOfficeCommand>
{
    public UpdateOutOfOfficeValidator()
    {
        RuleFor(command => command.Reason).MaximumLength(120);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command).Must(command => command.EndDate >= command.StartDate)
            .WithMessage("Out-of-office end date must be on or after start date.")
            .WithName("endDate");
    }
}

public sealed class UpdateOutOfOfficeHandler(
    IOutOfOfficeRepository outOfOfficeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateOutOfOfficeCommand, Result<OutOfOfficeResponse>>
{
    public async Task<Result<OutOfOfficeResponse>> Handle(UpdateOutOfOfficeCommand command, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        if (callerId is null) return Result<OutOfOfficeResponse>.Unauthorized("Authentication is required.");
        if (callerId != command.UserId) return Result<OutOfOfficeResponse>.Forbidden("Out-of-office entries can only be managed by their owner.");

        var entry = await outOfOfficeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null || entry.UserId != command.UserId)
        {
            return Result<OutOfOfficeResponse>.NotFound($"Out-of-office '{command.Id}' was not found.");
        }

        try
        {
            entry.Update(command.StartDate, command.EndDate, command.ToUserId, command.Reason, command.Notes);
        }
        catch (ArgumentException exception)
        {
            return Result<OutOfOfficeResponse>.BadRequest(exception.Message);
        }

        outOfOfficeRepository.Update(entry);
        events.CollectEvent(new OutOfOfficeUpdated(entry.Id));

        return OutOfOfficeResponse.From(entry);
    }
}

// ─── Delete ──────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Delete)]
public sealed record DeleteOutOfOfficeCommand(UserId UserId, OutOfOfficeId Id) : ICommand, IRequest<Result>;

public sealed class DeleteOutOfOfficeHandler(
    IOutOfOfficeRepository outOfOfficeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteOutOfOfficeCommand, Result>
{
    public async Task<Result> Handle(DeleteOutOfOfficeCommand command, CancellationToken cancellationToken)
    {
        var callerId = executionContext.UserInfo.Id;
        if (callerId is null) return Result.Unauthorized("Authentication is required.");
        if (callerId != command.UserId) return Result.Forbidden("Out-of-office entries can only be managed by their owner.");

        var entry = await outOfOfficeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null || entry.UserId != command.UserId)
        {
            return Result.NotFound($"Out-of-office '{command.Id}' was not found.");
        }

        outOfOfficeRepository.Remove(entry);
        events.CollectEvent(new OutOfOfficeDeleted(entry.Id));

        return Result.Success();
    }
}
