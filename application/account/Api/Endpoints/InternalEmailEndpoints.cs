using Account.Features.Email.Commands;
using Account.Features.Email.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.Integrations.Email;

namespace Account.Api.Endpoints;

public sealed class InternalEmailEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/internal-api/account/email/messages";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Internal email");

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
