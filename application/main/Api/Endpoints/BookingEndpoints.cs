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

        group.MapGet("/{id}", async Task<ApiResult<BookingDetailsResponse>> (BookingId id, IMediator mediator)
            => await mediator.Send(new GetBookingDetailsQuery(id))
        ).Produces<BookingDetailsResponse>();

        group.MapGet("/{id}/attendees", async Task<ApiResult<BookingAttendeesResponse>> (BookingId id, IMediator mediator)
            => await mediator.Send(new GetBookingAttendeesQuery(id))
        ).Produces<BookingAttendeesResponse>();

        group.MapGet("/{id}/history", async Task<ApiResult<BookingHistoryResponse>> (BookingId id, [AsParameters] GetBookingHistoryQuery query, IMediator mediator)
            => await mediator.Send(query with { Id = id })
        ).Produces<BookingHistoryResponse>();

        group.MapPost("/{id}/cancel", async Task<ApiResult> (BookingId id, string? reason, IMediator mediator)
            => await mediator.Send(new CancelBookingCommand(id, reason))
        );

        group.MapPost("/{id}/confirm", async Task<ApiResult> (BookingId id, IMediator mediator)
            => await mediator.Send(new ConfirmBookingCommand(id))
        );

        group.MapPost("/{id}/reject", async Task<ApiResult> (BookingId id, RejectBookingCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/request-reschedule", async Task<ApiResult> (BookingId id, RequestRescheduleCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/location", async Task<ApiResult> (BookingId id, EditBookingLocationCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/guests", async Task<ApiResult> (BookingId id, AddBookingGuestsCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/no-show", async Task<ApiResult> (BookingId id, MarkNoShowCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/reassign", async Task<ApiResult> (BookingId id, ReassignBookingCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/rate", async Task<ApiResult> (BookingId id, RateBookingCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapPost("/{id}/notes", async Task<ApiResult<BookingInternalNoteId>> (BookingId id, AddBookingInternalNoteCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<BookingInternalNoteId>();

        group.MapDelete("/{id}/notes/{noteId}", async Task<ApiResult> (BookingId id, BookingInternalNoteId noteId, IMediator mediator)
            => await mediator.Send(new DeleteBookingInternalNoteCommand(id, noteId))
        );

        group.MapPost("/{id}/seats", async Task<ApiResult<ReserveBookingSeatResponse>> (BookingId id, ReserveBookingSeatCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<ReserveBookingSeatResponse>();

        group.MapDelete("/{id}/seats/{seatId}", async Task<ApiResult> (BookingId id, BookingSeatId seatId, IMediator mediator)
            => await mediator.Send(new ReleaseBookingSeatCommand(id, seatId))
        );

        group.MapPost("/{id}/report", async Task<ApiResult<BookingReportId>> (BookingId id, ReportBookingCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<BookingReportId>();

        group.MapGet("/reports", async Task<ApiResult<BookingReportsResponse>> ([AsParameters] GetReportsForTenantQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BookingReportsResponse>();
    }
}

public sealed record RejectBookingRequest(string? RejectionReason = null);

public sealed record RequestRescheduleRequest(string? RescheduleReason = null);

public sealed record EditBookingLocationRequest(string? LocationType, string? LocationValue);

public sealed record AddBookingGuestsRequest(BookingGuestRequest[] Guests);
