using JetBrains.Annotations;
using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Books an appointment on behalf of an identified WhatsApp customer (the receptionist's
///     CreateBooking tool). Booker identity comes exclusively from server-side conversation state; the
///     model supplies only the service slug and the slot start time. Composes the existing public booking
///     command so every validation rule (availability, limits, buffers, confirmation policy) applies.
/// </summary>
[PublicAPI]
public sealed record CreateCustomerBookingCommand(
    TenantId TenantId,
    string CustomerPhoneNumber,
    string CustomerName,
    string CustomerEmail,
    string ServiceSlug,
    DateTimeOffset StartTime
) : ICommand, IRequest<Result<CreatePublicBookingResponse>>;

public sealed class CreateCustomerBookingHandler(
    ISchedulingProfileRepository schedulingProfileRepository,
    IMediator mediator,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateCustomerBookingCommand, Result<CreatePublicBookingResponse>>
{
    public async Task<Result<CreatePublicBookingResponse>> Handle(CreateCustomerBookingCommand command, CancellationToken cancellationToken)
    {
        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (profile is null)
        {
            return Result<CreatePublicBookingResponse>.BadRequest("Online booking is not available for this business.");
        }

        var eventTypeResult = await mediator.Send(new Queries.GetPublicServicesQuery(command.TenantId), cancellationToken);
        var service = eventTypeResult.IsSuccess ? eventTypeResult.Value!.Services.FirstOrDefault(s => s.Slug == command.ServiceSlug) : null;
        if (service is null)
        {
            return Result<CreatePublicBookingResponse>.BadRequest($"Service '{command.ServiceSlug}' was not found.");
        }

        var createCommand = new CreatePublicBookingCommand(
            profile.Handle,
            command.ServiceSlug,
            command.StartTime,
            service.DurationMinutes,
            "Africa/Johannesburg",
            command.CustomerName,
            command.CustomerEmail,
            BookerPhone: command.CustomerPhoneNumber
        );

        var result = await mediator.Send(createCommand, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return result;
        }

        events.CollectEvent(new BookingCreatedByAgent(result.Value.Id, service.Id));

        return result;
    }
}
