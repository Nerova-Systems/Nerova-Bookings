using Account.Features.Attributes;
using Account.Features.Attributes.Commands.AssignAttribute;
using Account.Features.Attributes.Commands.CreateAttribute;
using Account.Features.Attributes.Commands.CreateAttributeOption;
using Account.Features.Attributes.Commands.DeleteAttribute;
using Account.Features.Attributes.Commands.DeleteAttributeOption;
using Account.Features.Attributes.Commands.UnassignAttribute;
using Account.Features.Attributes.Commands.UpdateAttribute;
using Account.Features.Attributes.Commands.UpdateAttributeOption;
using Account.Features.Attributes.Domain;
using Account.Features.Attributes.Queries.GetMembershipAttributes;
using Account.Features.Attributes.Queries.ListOrgAttributes;
using Account.Features.Memberships.Domain;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class AttributeEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/org";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ─── Org-level attribute management ──────────────────────────────────

        var attrGroup = routes
            .MapGroup($"{RoutesPrefix}/attributes")
            .WithTags("Attributes")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        attrGroup.MapGet("/", async Task<ApiResult<AttributeResponse[]>> (IMediator mediator)
            => await mediator.Send(new ListOrgAttributesQuery())
        ).Produces<AttributeResponse[]>();

        attrGroup.MapPost("/", async Task<ApiResult<AttributeResponse>> (CreateAttributeCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<AttributeResponse>();

        attrGroup.MapPut("/{id}", async Task<ApiResult<AttributeResponse>> (
                AttributeId id,
                UpdateAttributeCommand command,
                IMediator mediator)
            => await mediator.Send(command with { AttributeId = id })
        ).Produces<AttributeResponse>();

        attrGroup.MapDelete("/{id}", async Task<ApiResult> (AttributeId id, IMediator mediator)
            => await mediator.Send(new DeleteAttributeCommand { AttributeId = id })
        );

        // ─── Option management ────────────────────────────────────────────────

        attrGroup.MapPost("/{id}/options", async Task<ApiResult<AttributeOptionResponse>> (
                AttributeId id,
                CreateAttributeOptionCommand command,
                IMediator mediator)
            => await mediator.Send(command with { AttributeId = id })
        ).Produces<AttributeOptionResponse>();

        attrGroup.MapPut("/{id}/options/{optionId}", async Task<ApiResult<AttributeOptionResponse>> (
                AttributeId id,
                AttributeOptionId optionId,
                UpdateAttributeOptionCommand command,
                IMediator mediator)
            => await mediator.Send(command with { AttributeId = id, OptionId = optionId })
        ).Produces<AttributeOptionResponse>();

        attrGroup.MapDelete("/{id}/options/{optionId}", async Task<ApiResult> (
                AttributeId id,
                AttributeOptionId optionId,
                IMediator mediator)
            => await mediator.Send(new DeleteAttributeOptionCommand { AttributeId = id, OptionId = optionId })
        );

        // ─── Member attribute assignments ─────────────────────────────────────

        var memberGroup = routes
            .MapGroup($"{RoutesPrefix}/members")
            .WithTags("Attributes")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        memberGroup.MapGet("/{membershipId}/attributes", async Task<ApiResult<AttributeAssignmentResponse[]>> (
                MembershipId membershipId,
                IMediator mediator)
            => await mediator.Send(new GetMembershipAttributesQuery { MembershipId = membershipId })
        ).Produces<AttributeAssignmentResponse[]>();

        memberGroup.MapPut("/{membershipId}/attributes/{attributeId}", async Task<ApiResult<AttributeAssignmentResponse>> (
                MembershipId membershipId,
                AttributeId attributeId,
                AssignAttributeCommand command,
                IMediator mediator)
            => await mediator.Send(command with { MembershipId = membershipId, AttributeId = attributeId })
        ).Produces<AttributeAssignmentResponse>();

        memberGroup.MapDelete("/{membershipId}/attributes/{attributeId}", async Task<ApiResult> (
                MembershipId membershipId,
                AttributeId attributeId,
                AttributeOptionId? optionId,
                IMediator mediator)
            => await mediator.Send(new UnassignAttributeCommand
            {
                MembershipId = membershipId,
                AttributeId = attributeId,
                OptionId = optionId
            })
        );
    }
}
