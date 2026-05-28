using Main.Features.Schedules.Commands;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Queries;
using Main.Features.Schedules.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;

namespace Main.Api.Endpoints;

/// <summary>
///     Team-scoped wrappers around the schedule endpoints. Authorization requires the
///     caller's active team (set when switching tenant) to match the <c>teamId</c> route
///     parameter. The underlying handlers already key off <c>ActiveTeamId</c> for scoping.
/// </summary>
public sealed class TeamScheduleEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/teams/{teamId}/schedules";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Team Schedules").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<SchedulesResponse>> (TenantId teamId, IExecutionContext executionContext, IMediator mediator) =>
            {
                if (!TeamAccess.IsAuthorized(executionContext, teamId)) return Result<SchedulesResponse>.Forbidden(TeamAccess.ForbiddenMessage);
                return await mediator.Send(new GetSchedulesQuery());
            }
        ).Produces<SchedulesResponse>();

        group.MapPost("/", async Task<ApiResult<ScheduleResponse>> (TenantId teamId, CreateScheduleCommand command, IExecutionContext executionContext, IMediator mediator) =>
            {
                if (!TeamAccess.IsAuthorized(executionContext, teamId)) return Result<ScheduleResponse>.Forbidden(TeamAccess.ForbiddenMessage);
                return await mediator.Send(command);
            }
        ).Produces<ScheduleResponse>();

        group.MapGet("/{scheduleId}", async Task<ApiResult<ScheduleResponse>> (TenantId teamId, ScheduleId scheduleId, IExecutionContext executionContext, IMediator mediator) =>
            {
                if (!TeamAccess.IsAuthorized(executionContext, teamId)) return Result<ScheduleResponse>.Forbidden(TeamAccess.ForbiddenMessage);
                return await mediator.Send(new GetScheduleQuery(scheduleId));
            }
        ).Produces<ScheduleResponse>();

        group.MapPatch("/{scheduleId}", async Task<ApiResult<ScheduleResponse>> (TenantId teamId, ScheduleId scheduleId, UpdateScheduleCommand command, IExecutionContext executionContext, IMediator mediator) =>
            {
                if (!TeamAccess.IsAuthorized(executionContext, teamId)) return Result<ScheduleResponse>.Forbidden(TeamAccess.ForbiddenMessage);
                return await mediator.Send(command with { Id = scheduleId });
            }
        ).Produces<ScheduleResponse>();

        group.MapDelete("/{scheduleId}", async Task<ApiResult> (TenantId teamId, ScheduleId scheduleId, IExecutionContext executionContext, IMediator mediator) =>
            {
                if (!TeamAccess.IsAuthorized(executionContext, teamId)) return Result.Forbidden(TeamAccess.ForbiddenMessage);
                return await mediator.Send(new DeleteScheduleCommand(scheduleId));
            }
        );
    }
}

internal static class TeamAccess
{
    public const string ForbiddenMessage = "Caller is not a member of the specified team.";

    public static bool IsAuthorized(IExecutionContext executionContext, TenantId teamId)
    {
        return executionContext.ActiveTeamId is not null && executionContext.ActiveTeamId == teamId;
    }
}
