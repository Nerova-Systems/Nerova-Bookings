using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record AddWorkflowStepCommand(
    WorkflowId WorkflowId,
    WorkflowAction Action,
    WorkflowReminderTemplate Template,
    int? ReminderTime,
    WorkflowTimeUnit? TimeUnit,
    string? SendTo,
    string? EmailSubject,
    string? EmailBody
) : ICommand, IRequest<Result<WorkflowStepResponse>>;

public sealed class AddWorkflowStepValidator : AbstractValidator<AddWorkflowStepCommand>
{
    public AddWorkflowStepValidator()
    {
        RuleFor(c => c.SendTo).MaximumLength(320);
        RuleFor(c => c.EmailSubject).MaximumLength(500);
        RuleFor(c => c.EmailBody).MaximumLength(5000);
        RuleFor(c => c.ReminderTime)
            .InclusiveBetween(1, 100000)
            .When(c => c.ReminderTime.HasValue);
    }
}

public sealed class AddWorkflowStepHandler(
    IWorkflowRepository workflowRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<AddWorkflowStepCommand, Result<WorkflowStepResponse>>
{
    public async Task<Result<WorkflowStepResponse>> Handle(AddWorkflowStepCommand command, CancellationToken cancellationToken)
    {
        if (!WorkflowAuthorization.CanManageWorkflows(executionContext.UserInfo))
        {
            return Result<WorkflowStepResponse>.Forbidden(WorkflowAuthorization.ManageWorkflowsForbiddenMessage);
        }

        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result<WorkflowStepResponse>.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<WorkflowStepResponse>.Unauthorized("Authentication is required.");
        }

        var workflow = await workflowRepository.GetByIdWithStepsAsync(command.WorkflowId, cancellationToken);
        if (workflow is null || workflow.OwnerUserId != ownerUserId)
        {
            return Result<WorkflowStepResponse>.NotFound($"Workflow '{command.WorkflowId}' was not found.");
        }

        var step = workflow.AddStep(command.Action, command.Template, command.ReminderTime, command.TimeUnit, command.SendTo, command.EmailSubject, command.EmailBody);
        workflowRepository.TrackNewStep(workflow, step);
        events.CollectEvent(new WorkflowStepAdded(workflow.Id, step.Id));

        return WorkflowStepResponse.From(step);
    }
}
