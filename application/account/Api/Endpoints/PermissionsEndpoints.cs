using Account.Features.Memberships.Domain;
using Account.Features.Permissions;
using Account.Features.Permissions.Commands.AssignRoleToMembership;
using Account.Features.Permissions.Commands.CreateRole;
using Account.Features.Permissions.Commands.DeleteRole;
using Account.Features.Permissions.Commands.UpdateRole;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Queries.GetAllPermissions;
using Account.Features.Permissions.Queries.GetRoleById;
using Account.Features.Permissions.Queries.GetRolesForTenant;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

/// <summary>
///     HTTP endpoints for PBAC (Permission-Based Access Control) management. Backs the
///     <c>u4-pbac-admin</c> UI: organization administrators may create, edit, and delete
///     custom roles and assign them to members. All endpoints are gated by the
///     <c>tier-enterprise</c> feature flag inside their respective handlers.
/// </summary>
public sealed class PermissionsEndpoints : IEndpoints
{
    private const string RolesRoutesPrefix = "/api/account/roles";
    private const string MembershipsRoutesPrefix = "/api/account/memberships";
    private const string PermissionsRoutesPrefix = "/api/account/permissions";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ─── Role management ──────────────────────────────────────────────────

        var roleGroup = routes
            .MapGroup(RolesRoutesPrefix)
            .WithTags("Permissions")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        roleGroup.MapGet("/", async Task<ApiResult<RoleResponse[]>> (IMediator mediator)
            => await mediator.Send(new GetRolesForTenantQuery())
        ).Produces<RoleResponse[]>();

        roleGroup.MapGet("/{id}", async Task<ApiResult<RoleResponse>> (RoleId id, IMediator mediator)
            => await mediator.Send(new GetRoleByIdQuery { RoleId = id })
        ).Produces<RoleResponse>();

        roleGroup.MapPost("/", async Task<ApiResult<RoleResponse>> (CreateRoleCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<RoleResponse>();

        roleGroup.MapPut("/{id}", async Task<ApiResult<RoleResponse>> (
                RoleId id,
                UpdateRoleCommand command,
                IMediator mediator)
            => await mediator.Send(command with { RoleId = id })
        ).Produces<RoleResponse>();

        roleGroup.MapDelete("/{id}", async Task<ApiResult> (RoleId id, IMediator mediator)
            => await mediator.Send(new DeleteRoleCommand { RoleId = id })
        );

        // ─── Membership role assignment ───────────────────────────────────────

        var membershipGroup = routes
            .MapGroup(MembershipsRoutesPrefix)
            .WithTags("Permissions")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        membershipGroup.MapPut("/{id}/role", async Task<ApiResult> (
                MembershipId id,
                AssignRoleToMembershipCommand command,
                IMediator mediator)
            => await mediator.Send(command with { MembershipId = id })
        );

        // ─── Permission catalog ───────────────────────────────────────────────

        var permissionGroup = routes
            .MapGroup(PermissionsRoutesPrefix)
            .WithTags("Permissions")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        permissionGroup.MapGet("/", async Task<ApiResult<PermissionGroupResponse[]>> (IMediator mediator)
            => await mediator.Send(new GetAllPermissionsQuery())
        ).Produces<PermissionGroupResponse[]>();
    }
}
