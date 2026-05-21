using Main.Features.EventTypes.Domain;
using Main.Features.Workflows.Commands;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Queries;
using Main.Features.Workflows.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class WorkflowEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/workflows";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Workflows").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<WorkflowsResponse>> (IMediator mediator)
            => await mediator.Send(new GetWorkflowsQuery())
        ).Produces<WorkflowsResponse>();

        group.MapGet("/{id}", async Task<ApiResult<WorkflowResponse>> (WorkflowId id, IMediator mediator)
            => await mediator.Send(new GetWorkflowQuery(id))
        ).Produces<WorkflowResponse>();

        group.MapPost("/", async Task<ApiResult<WorkflowResponse>> (CreateWorkflowCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<WorkflowResponse>();

        group.MapPut("/{id}", async Task<ApiResult<WorkflowResponse>> (WorkflowId id, UpdateWorkflowCommand command, IMediator mediator)
            => await mediator.Send(command with { WorkflowId = id })
        ).Produces<WorkflowResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (WorkflowId id, IMediator mediator)
            => await mediator.Send(new DeleteWorkflowCommand(id))
        );

        // Steps
        group.MapPost("/{id}/steps", async Task<ApiResult<WorkflowStepResponse>> (WorkflowId id, AddWorkflowStepCommand command, IMediator mediator)
            => await mediator.Send(command with { WorkflowId = id })
        ).Produces<WorkflowStepResponse>();

        group.MapPut("/{id}/steps/{stepId}", async Task<ApiResult<WorkflowStepResponse>> (WorkflowId id, WorkflowStepId stepId, UpdateWorkflowStepCommand command, IMediator mediator)
            => await mediator.Send(command with { WorkflowId = id, StepId = stepId })
        ).Produces<WorkflowStepResponse>();

        group.MapDelete("/{id}/steps/{stepId}", async Task<ApiResult> (WorkflowId id, WorkflowStepId stepId, IMediator mediator)
            => await mediator.Send(new DeleteWorkflowStepCommand(id, stepId))
        );

        // Bindings
        group.MapPost("/{id}/bindings", async Task<ApiResult<WorkflowEventTypeBindingResponse>> (WorkflowId id, BindWorkflowToEventTypeCommand command, IMediator mediator)
            => await mediator.Send(command with { WorkflowId = id })
        ).Produces<WorkflowEventTypeBindingResponse>();

        group.MapDelete("/{id}/bindings/{eventTypeId}", async Task<ApiResult> (WorkflowId id, EventTypeId eventTypeId, IMediator mediator)
            => await mediator.Send(new UnbindWorkflowFromEventTypeCommand(id, eventTypeId))
        );
    }
}
