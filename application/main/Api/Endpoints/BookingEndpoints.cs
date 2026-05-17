using Main.Features.Scheduling.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class BookingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/bookings";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Bookings").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<BookingsResponse>> ([AsParameters] GetBookingsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BookingsResponse>();
    }
}
