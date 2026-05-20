using Main.Features.Connectors.Commands;
using Main.Features.Connectors.Domain;
using Main.Features.Connectors.Queries;
using Main.Features.Connectors.Shared;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class CoreConnectorEndpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var connectorGroup = routes.MapGroup("/api/connectors/core")
            .WithTags("CoreConnectors")
            .RequireAuthorization()
            .ProducesValidationProblem();

        connectorGroup.MapGet("/accounts", async Task<ApiResult<CoreConnectorAccountsResponse>> (IMediator mediator)
            => await mediator.Send(new GetCoreConnectorAccountsQuery())
        ).Produces<CoreConnectorAccountsResponse>();

        connectorGroup.MapGet("/{integration}/authorization-url", async Task<ApiResult<CoreConnectorAuthorizationUrlResponse>> (
                string integration,
                string? returnTo,
                IMediator mediator
            ) => await mediator.Send(new GetCoreConnectorAuthorizationUrlQuery(integration, returnTo))
        ).Produces<CoreConnectorAuthorizationUrlResponse>();

        connectorGroup.MapGet("/{integration}/callback", async Task<ApiResult<string>> (
                string integration,
                string? code,
                string? state,
                IMediator mediator
            ) => await mediator.Send(new CompleteCoreConnectorOAuthCallbackCommand(integration, code, state))
        ).ExcludeFromDescription();

        connectorGroup.MapDelete("/accounts/{credentialId}", async Task<ApiResult> (string credentialId, IMediator mediator)
            => await mediator.Send(new DeleteCoreConnectorAccountCommand(credentialId))
        );

        connectorGroup.MapPost("/test-fixtures", async Task<ApiResult<CoreConnectorAccountsResponse>> (EnsureTestCoreConnectorCredentialsRequest request, IMediator mediator)
                => await mediator.Send(new EnsureTestCoreConnectorCredentialsCommand(request.BusyStartTime, request.BusyEndTime))
            ).Produces<CoreConnectorAccountsResponse>()
            .ExcludeFromDescription();

        var eventTypeGroup = routes.MapGroup("/api/event-types/{eventTypeId}/connector-settings")
            .WithTags("CoreConnectors")
            .RequireAuthorization()
            .ProducesValidationProblem();

        eventTypeGroup.MapPut("/selected-calendars", async Task<ApiResult<EventTypeResponse>> (EventTypeId eventTypeId, UpdateSelectedCalendarsRequest request, IMediator mediator)
            => await mediator.Send(new UpdateSelectedCalendarsCommand(eventTypeId, request.SelectedCalendars))
        ).Produces<EventTypeResponse>();

        eventTypeGroup.MapPut("/destination-calendar", async Task<ApiResult<EventTypeResponse>> (EventTypeId eventTypeId, UpdateDestinationCalendarRequest request, IMediator mediator)
            => await mediator.Send(new UpdateDestinationCalendarCommand(eventTypeId, request.DestinationCalendar))
        ).Produces<EventTypeResponse>();

        eventTypeGroup.MapPut("/default-conferencing", async Task<ApiResult<EventTypeResponse>> (EventTypeId eventTypeId, UpdateDefaultConferencingRequest request, IMediator mediator)
            => await mediator.Send(new UpdateDefaultConferencingCommand(eventTypeId, request.DefaultConferencing))
        ).Produces<EventTypeResponse>();
    }
}

public sealed record EnsureTestCoreConnectorCredentialsRequest(DateTimeOffset BusyStartTime, DateTimeOffset BusyEndTime);
