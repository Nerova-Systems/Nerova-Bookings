using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Commands.AssignManagedEventType;
using Main.Features.ManagedEventTypes.Commands.SyncManagedEventType;
using Main.Features.ManagedEventTypes.Commands.UnassignManagedEventType;
using Main.Features.ManagedEventTypes.Commands.UpdateManagedEventTypeLocks;
using Main.Features.ManagedEventTypes.Queries.GetManagedEventTypeAssignmentStatus;
using Main.Features.ManagedEventTypes.Queries.ListManagedEventTypeChildren;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class ManagedEventTypeEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/managed-event-types";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("ManagedEventTypes").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/{parentId}/children", async Task<ApiResult<ManagedEventTypeChildrenResponse>> (EventTypeId parentId, IMediator mediator)
            => await mediator.Send(new ListManagedEventTypeChildrenQuery(parentId))
        ).Produces<ManagedEventTypeChildrenResponse>();

        group.MapGet("/{parentId}/status", async Task<ApiResult<ManagedEventTypeAssignmentStatusResponse>> (EventTypeId parentId, IMediator mediator)
            => await mediator.Send(new GetManagedEventTypeAssignmentStatusQuery(parentId))
        ).Produces<ManagedEventTypeAssignmentStatusResponse>();

        group.MapPost("/{parentId}/assignments", async Task<ApiResult> (EventTypeId parentId, AssignManagedEventTypeRequest request, IMediator mediator)
            => await mediator.Send(new AssignManagedEventTypeCommand(parentId, request.MemberUserId))
        );

        group.MapDelete("/{parentId}/assignments/{memberUserId}", async Task<ApiResult> (EventTypeId parentId, UserId memberUserId, IMediator mediator)
            => await mediator.Send(new UnassignManagedEventTypeCommand(parentId, memberUserId))
        );

        group.MapPost("/{parentId}/sync", async Task<ApiResult> (EventTypeId parentId, IMediator mediator)
            => await mediator.Send(new SyncManagedEventTypeCommand(parentId))
        );

        group.MapPut("/{parentId}/locks", async Task<ApiResult> (EventTypeId parentId, UpdateManagedEventTypeLocksRequest request, IMediator mediator)
            => await mediator.Send(new UpdateManagedEventTypeLocksCommand(parentId, request.UnlockedFields))
        );
    }
}
