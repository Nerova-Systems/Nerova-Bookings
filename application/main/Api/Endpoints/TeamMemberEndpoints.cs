using Main.Features.TeamMembers.Queries;
using Main.Features.TeamMembers.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class TeamMemberEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/team-members";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("TeamMembers").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/search", async Task<ApiResult<SearchTeamMembersResponse>> (string? query, int? limit, IMediator mediator)
            => await mediator.Send(new SearchTeamMembersQuery(query, limit ?? 20))
        ).Produces<SearchTeamMembersResponse>();
    }
}
