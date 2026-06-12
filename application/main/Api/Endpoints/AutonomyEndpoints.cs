using Main.Features.Autonomy.Commands;
using Main.Features.Autonomy.Domain;
using Main.Features.Autonomy.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class AutonomyEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/autonomy";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Autonomy").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/job-runs", async Task<ApiResult<GetJobRunsResponse>> ([AsParameters] GetJobRunsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetJobRunsResponse>();

        group.MapPost("/job-runs/{id}/approve", async Task<ApiResult<ApproveJobRunResponse>> (JobRunId id, IMediator mediator)
            => await mediator.Send(new ApproveJobRunCommand { Id = id })
        ).Produces<ApproveJobRunResponse>();

        group.MapPost("/job-runs/{id}/dismiss", async Task<ApiResult> (JobRunId id, IMediator mediator)
            => await mediator.Send(new DismissJobRunCommand { Id = id })
        );

        group.MapGet("/policies", async Task<ApiResult<GetJobPoliciesResponse>> ([AsParameters] GetJobPoliciesQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetJobPoliciesResponse>();

        group.MapPut("/policies", async Task<ApiResult> (SetJobPolicyLevelCommand command, IMediator mediator)
            => await mediator.Send(command)
        );
    }
}
