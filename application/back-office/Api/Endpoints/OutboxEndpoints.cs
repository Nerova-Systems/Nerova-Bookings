using BackOffice.Features.Outbox.Commands;
using BackOffice.Features.Outbox.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Authorization;
using SharedKernel.Endpoints;

namespace BackOffice.Api.Endpoints;

public sealed class OutboxEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/outbox/messages";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Outbox").RequireAuthorization(SysOpAuthorization.PolicyName).ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<OutboxMessagesResponse>> ([AsParameters] GetOutboxMessagesQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<OutboxMessagesResponse>();

        group.MapPost("/{id:guid}/retry", async Task<ApiResult> (Guid id, IMediator mediator)
            => await mediator.Send(new RetryOutboxMessageCommand(id))
        );
    }
}
