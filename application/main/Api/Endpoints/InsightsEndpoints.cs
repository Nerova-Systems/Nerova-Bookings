using Main.Features.Insights.Queries.GetBookingFunnel;
using Main.Features.Insights.Queries.GetBookingHeatmap;
using Main.Features.Insights.Queries.GetBookingKpis;
using Main.Features.Insights.Queries.GetBookingsOverTime;
using Main.Features.Insights.Queries.GetCancellationReasons;
using Main.Features.Insights.Queries.GetTopEventTypes;
using Main.Features.Insights.Queries.GetTopHosts;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class InsightsEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/insights";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Insights").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/kpis", async Task<ApiResult<BookingKpisResponse>> ([AsParameters] GetBookingKpisQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BookingKpisResponse>();

        group.MapGet("/bookings-over-time", async Task<ApiResult<BookingsOverTimeResponse>> ([AsParameters] GetBookingsOverTimeQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BookingsOverTimeResponse>();

        group.MapGet("/top-event-types", async Task<ApiResult<TopEventTypesResponse>> ([AsParameters] GetTopEventTypesQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<TopEventTypesResponse>();

        group.MapGet("/top-hosts", async Task<ApiResult<TopHostsResponse>> ([AsParameters] GetTopHostsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<TopHostsResponse>();

        group.MapGet("/heatmap", async Task<ApiResult<BookingHeatmapResponse>> ([AsParameters] GetBookingHeatmapQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BookingHeatmapResponse>();

        group.MapGet("/funnel", async Task<ApiResult<BookingFunnelResponse>> ([AsParameters] GetBookingFunnelQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BookingFunnelResponse>();

        group.MapGet("/cancellation-reasons", async Task<ApiResult<CancellationReasonsResponse>> ([AsParameters] GetCancellationReasonsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<CancellationReasonsResponse>();
    }
}
