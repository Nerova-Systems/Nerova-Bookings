using Main.Features.DataImport.Commands;
using Main.Features.DataImport.Domain;
using Main.Features.DataImport.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class ImportJobEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/import-jobs";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("ImportJobs").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<GetImportJobsResponse>> ([AsParameters] GetImportJobsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetImportJobsResponse>();

        group.MapGet("/{id}", async Task<ApiResult<ImportJobDetailsResponse>> (ImportJobId id, IMediator mediator)
            => await mediator.Send(new GetImportJobQuery { Id = id })
        ).Produces<ImportJobDetailsResponse>();

        group.MapPost("/", async Task<ApiResult<StartImportJobResponse>> (StartImportJobCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<StartImportJobResponse>();

        group.MapPost("/{id}/approve", async Task<ApiResult> (ImportJobId id, ApproveImportJobCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/reject", async Task<ApiResult> (ImportJobId id, IMediator mediator)
            => await mediator.Send(new RejectImportJobCommand { Id = id })
        );

        group.MapPost("/{id}/run", async Task<ApiResult> (ImportJobId id, IMediator mediator)
            => await mediator.Send(new RunImportPipelineCommand(id))
        );
    }
}
