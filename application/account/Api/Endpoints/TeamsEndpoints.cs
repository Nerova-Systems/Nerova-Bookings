using Account.Features.Memberships.Domain;
using Account.Features.Teams;
using Account.Features.Teams.Commands;
using Account.Features.Teams.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

/// <summary>
///     HTTP endpoints for team management. Mirrors cal.com's <c>viewer.teams.*</c> tRPC procedures
///     translated to REST. All endpoints are gated by the <c>tier-teams</c> feature flag inside
///     their respective handlers.
/// </summary>
public sealed class TeamsEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/teams";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutesPrefix)
            .WithTags("Teams")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<TeamResponse[]>> (IMediator mediator)
            => await mediator.Send(new GetTeamsInOrgQuery())
        ).Produces<TeamResponse[]>();

        group.MapGet("/{id}", async Task<ApiResult<TeamResponse>> (TenantId id, IMediator mediator)
            => await mediator.Send(new GetTeamByIdQuery(id))
        ).Produces<TeamResponse>();

        group.MapGet("/{id}/members", async Task<ApiResult<TeamMemberResponse[]>> (TenantId id, IMediator mediator)
            => await mediator.Send(new GetTeamMembersQuery(id))
        ).Produces<TeamMemberResponse[]>();

        group.MapPost("/", async Task<ApiResult<TeamResponse>> (CreateTeamCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<TeamResponse>();

        group.MapPut("/{id}", async Task<ApiResult<TeamResponse>> (TenantId id, UpdateTeamCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<TeamResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (TenantId id, IMediator mediator)
            => await mediator.Send(new DeleteTeamCommand(id))
        );

        group.MapPut("/{id}/members", async Task<ApiResult> (TenantId id, UpdateTeamMembersCommand command, IMediator mediator)
            => await mediator.Send(command with { TeamId = id })
        );

        group.MapPost("/{id}/invitations", async Task<ApiResult<MembershipId>> (TenantId id, InviteTeamMemberCommand command, IMediator mediator)
            => await mediator.Send(command with { TeamId = id })
        ).Produces<MembershipId>();
    }
}
