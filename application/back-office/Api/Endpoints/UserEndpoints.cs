using BackOffice.Features.Users.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Authorization;
using SharedKernel.Endpoints;

namespace BackOffice.Api.Endpoints;

public sealed class UserEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/users";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Users").RequireAuthorization(SysOpAuthorization.PolicyName).ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<UsersResponse>> ([AsParameters] GetUsersQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<UsersResponse>();
    }
}
