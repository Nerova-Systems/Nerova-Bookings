using BackOffice.Features.Messaging.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Authorization;
using SharedKernel.Endpoints;

namespace BackOffice.Api.Endpoints;

public sealed class MessagingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/messaging";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Messaging").RequireAuthorization(SysOpAuthorization.PolicyName).ProducesValidationProblem();

        group.MapGet("/health", async Task<ApiResult<MessagingHealthResponse>> (IMediator mediator)
            => await mediator.Send(new GetMessagingHealthQuery())
        ).Produces<MessagingHealthResponse>();
    }
}
