using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Queries;
using Main.Features.Scheduling.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class PublicSchedulingEndpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var profileGroup = routes.MapGroup("/api/scheduling/profile").WithTags("SchedulingProfile").RequireAuthorization().ProducesValidationProblem();

        profileGroup.MapGet("/", async Task<ApiResult<SchedulingProfileResponse>> ([AsParameters] GetSchedulingProfileQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<SchedulingProfileResponse>();

        profileGroup.MapPut("/", async Task<ApiResult<SchedulingProfileResponse>> (UpdateSchedulingProfileCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<SchedulingProfileResponse>();

        profileGroup.MapPut("/vertical", async Task<ApiResult> (SetSchedulingVerticalCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        var publicGroup = routes.MapGroup("/api/public").WithTags("PublicScheduling").ProducesValidationProblem();

        publicGroup.MapGet("/event-types/{handle}/{slug}", async Task<ApiResult<PublicEventTypeResponse>> (string handle, string slug, string? privateLink, IMediator mediator)
            => await mediator.Send(new GetPublicEventTypeQuery(handle, slug, privateLink))
        ).Produces<PublicEventTypeResponse>().AllowAnonymous();

        publicGroup.MapGet("/slots", async Task<ApiResult<PublicSlotsResponse>> ([AsParameters] GetPublicSlotsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<PublicSlotsResponse>().AllowAnonymous();

        publicGroup.MapGet("/reschedule-bookings/{id}", async Task<ApiResult<PublicRescheduleBookingResponse>> (BookingId id, string handle, string eventSlug, IMediator mediator)
            => await mediator.Send(new GetPublicRescheduleBookingQuery(id, handle, eventSlug))
        ).Produces<PublicRescheduleBookingResponse>().AllowAnonymous();

        publicGroup.MapPost("/bookings", async Task<ApiResult<CreatePublicBookingResponse>> (CreatePublicBookingCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<CreatePublicBookingResponse>().AllowAnonymous();
    }
}
