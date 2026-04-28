using BackOffice.Features.Email.Commands;
using BackOffice.Features.Email.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Authorization;
using SharedKernel.Endpoints;
using SharedKernel.Integrations.Email;

namespace BackOffice.Api.Endpoints;

public sealed class EmailEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/email/messages";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Email").RequireAuthorization(SysOpAuthorization.PolicyName).ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<TransactionalEmailMessagesResponse>> ([AsParameters] GetTransactionalEmailMessagesQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<TransactionalEmailMessagesResponse>();

        group.MapGet("/{id:guid}", async Task<ApiResult<TransactionalEmailMessageResponse>> (Guid id, IMediator mediator)
            => await mediator.Send(new GetTransactionalEmailMessageByIdQuery(id))
        ).Produces<TransactionalEmailMessageResponse>();

        group.MapPost("/{id:guid}/retry", async Task<ApiResult> (Guid id, IMediator mediator)
            => await mediator.Send(new RetryTransactionalEmailMessageCommand(id))
        );
    }
}
