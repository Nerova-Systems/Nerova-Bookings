using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
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

        group.MapPost("/{id}/cancel", async Task<ApiResult> (BookingId id, IMediator mediator)
            => await mediator.Send(new CancelBookingCommand(id))
        );

        group.MapPost("/{id}/confirm", async Task<ApiResult<BookingLifecycleResponse>> (BookingId id, IMediator mediator)
            => await mediator.Send(new ConfirmBookingCommand(id))
        ).Produces<BookingLifecycleResponse>();

        group.MapPost("/{id}/reject", async Task<ApiResult<BookingLifecycleResponse>> (BookingId id, RejectBookingRequest request, IMediator mediator)
            => await mediator.Send(new RejectBookingCommand(id, request.RejectionReason))
        ).Produces<BookingLifecycleResponse>();

        group.MapPost("/{id}/request-reschedule", async Task<ApiResult<BookingLifecycleResponse>> (BookingId id, RequestRescheduleRequest request, IMediator mediator)
            => await mediator.Send(new RequestRescheduleCommand(id, request.RescheduleReason))
        ).Produces<BookingLifecycleResponse>();

        group.MapPut("/{id}/location", async Task<ApiResult<BookingLifecycleResponse>> (BookingId id, EditBookingLocationRequest request, IMediator mediator)
            => await mediator.Send(new EditBookingLocationCommand(id, request.LocationType, request.LocationValue))
        ).Produces<BookingLifecycleResponse>();

        group.MapPost("/{id}/guests", async Task<ApiResult<BookingLifecycleResponse>> (BookingId id, AddBookingGuestsRequest request, IMediator mediator)
            => await mediator.Send(new AddBookingGuestsCommand(id, request.Guests))
        ).Produces<BookingLifecycleResponse>();
    }
}

public sealed record RejectBookingRequest(string? RejectionReason = null);

public sealed record RequestRescheduleRequest(string? RescheduleReason = null);

public sealed record EditBookingLocationRequest(string? LocationType, string? LocationValue);

public sealed record AddBookingGuestsRequest(BookingGuestRequest[] Guests);
