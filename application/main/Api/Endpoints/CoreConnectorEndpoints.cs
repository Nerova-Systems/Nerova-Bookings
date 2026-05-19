using Main.Features.Connectors.Commands;
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
        routes.MapGroup("/api/connectors/core")
            .WithTags("CoreConnectors")
            .RequireAuthorization()
            .ProducesValidationProblem()
            .MapGet("/accounts", async Task<ApiResult<CoreConnectorAccountsResponse>> (IMediator mediator)
                => await mediator.Send(new GetCoreConnectorAccountsQuery())
            )
            .Produces<CoreConnectorAccountsResponse>();

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
