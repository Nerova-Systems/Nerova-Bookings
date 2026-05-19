using FluentValidation;
using JetBrains.Annotations;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.BookingSideEffects.Commands;

[PublicAPI]
public sealed record CreateWorkflowCommand(
    EventTypeId EventTypeId,
    string Name,
    bool Active,
    string Trigger,
    int? ScheduledOffsetMinutes,
    WorkflowStep[] Steps
) : ICommand, IRequest<Result<WorkflowResponse>>;

[PublicAPI]
public sealed record UpdateWorkflowCommand(
    EventTypeId EventTypeId,
    WorkflowId Id,
    string Name,
    bool Active,
    string Trigger,
    int? ScheduledOffsetMinutes,
    WorkflowStep[] Steps
) : ICommand, IRequest<Result<WorkflowResponse>>;

[PublicAPI]
public sealed record DeleteWorkflowCommand(EventTypeId EventTypeId, WorkflowId Id) : ICommand, IRequest<Result>;

public sealed class CreateWorkflowValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(160);
        RuleFor(command => command.Trigger).Must(IsSupportedTrigger).WithMessage("Workflow trigger is not supported.");
        RuleFor(command => command.ScheduledOffsetMinutes).InclusiveBetween(-525600, 525600).When(command => command.ScheduledOffsetMinutes is not null);
        RuleFor(command => command.Steps).NotEmpty().WithMessage("At least one workflow step is required.");
        RuleForEach(command => command.Steps).ChildRules(step =>
            {
                step.RuleFor(s => s.Kind).NotEmpty().MaximumLength(80).Equal(BookingSideEffectConstants.EmailKind).WithMessage("Only email workflow steps are supported in this wave.");
                step.RuleFor(s => s.Recipient).NotEmpty().MaximumLength(80);
                step.RuleFor(s => s.Subject).MaximumLength(300);
                step.RuleFor(s => s.Body).MaximumLength(4000);
            }
        );
    }

    private static bool IsSupportedTrigger(string trigger)
    {
        return BookingSideEffectConstants.SupportedTriggers.Contains(trigger.Trim().ToUpperInvariant(), StringComparer.Ordinal);
    }
}

public sealed class UpdateWorkflowValidator : AbstractValidator<UpdateWorkflowCommand>
{
    public UpdateWorkflowValidator()
    {
        RuleFor(command => new CreateWorkflowCommand(command.EventTypeId, command.Name, command.Active, command.Trigger, command.ScheduledOffsetMinutes, command.Steps))
            .SetValidator(new CreateWorkflowValidator());
    }
}

public sealed class CreateWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<CreateWorkflowCommand, Result<WorkflowResponse>>
{
    public async Task<Result<WorkflowResponse>> Handle(CreateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWorkflows(executionContext);
        if (!authorization.IsSuccess) return Result<WorkflowResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<WorkflowResponse>.From(eventType);

        var workflow = Workflow.Create(
            executionContext.TenantId!,
            executionContext.UserInfo.Id!,
            command.EventTypeId,
            command.Name,
            command.Active,
            command.Trigger,
            command.ScheduledOffsetMinutes,
            command.Steps
        );
        await workflowRepository.AddAsync(workflow, cancellationToken);
        return WorkflowResponse.From(workflow);
    }
}

public sealed class UpdateWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<UpdateWorkflowCommand, Result<WorkflowResponse>>
{
    public async Task<Result<WorkflowResponse>> Handle(UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWorkflows(executionContext);
        if (!authorization.IsSuccess) return Result<WorkflowResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<WorkflowResponse>.From(eventType);

        var workflow = await workflowRepository.GetByIdAsync(command.Id, cancellationToken);
        if (workflow is null || workflow.EventTypeId != command.EventTypeId || workflow.OwnerUserId != executionContext.UserInfo.Id)
        {
            return Result<WorkflowResponse>.NotFound($"Workflow '{command.Id}' was not found.");
        }

        workflow.Update(command.Name, command.Active, command.Trigger, command.ScheduledOffsetMinutes, command.Steps);
        workflowRepository.Update(workflow);
        return WorkflowResponse.From(workflow);
    }
}

public sealed class DeleteWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<DeleteWorkflowCommand, Result>
{
    public async Task<Result> Handle(DeleteWorkflowCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWorkflows(executionContext);
        if (!authorization.IsSuccess) return authorization;

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result.From(eventType);

        var workflow = await workflowRepository.GetByIdAsync(command.Id, cancellationToken);
        if (workflow is null || workflow.EventTypeId != command.EventTypeId || workflow.OwnerUserId != executionContext.UserInfo.Id)
        {
            return Result.NotFound($"Workflow '{command.Id}' was not found.");
        }

        workflowRepository.Remove(workflow);
        return Result.Success();
    }
}
