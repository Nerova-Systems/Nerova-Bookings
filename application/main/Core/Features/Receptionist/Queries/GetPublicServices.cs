using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Receptionist.Queries;

[PublicAPI]
public sealed record PublicServiceResponse(
    EventTypeId Id,
    string Slug,
    string Title,
    string? Description,
    int DurationMinutes,
    decimal? Price,
    string Currency,
    decimal? DepositAmount,
    string? ImageUrl
);

[PublicAPI]
public sealed record GetPublicServicesResponse(PublicServiceResponse[] Services);

/// <summary>
///     Lists the bookable services of a tenant for anonymous channel surfaces (the AI receptionist and
///     WhatsApp Flows). Tenant identity comes from server-side conversation state.
/// </summary>
[PublicAPI]
public sealed record GetPublicServicesQuery(TenantId TenantId) : IRequest<Result<GetPublicServicesResponse>>;

public sealed class GetPublicServicesHandler(ISchedulingProfileRepository schedulingProfileRepository, IEventTypeRepository eventTypeRepository)
    : IRequestHandler<GetPublicServicesQuery, Result<GetPublicServicesResponse>>
{
    public async Task<Result<GetPublicServicesResponse>> Handle(GetPublicServicesQuery query, CancellationToken cancellationToken)
    {
        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(query.TenantId, cancellationToken);
        if (profile is null)
        {
            return Result<GetPublicServicesResponse>.Success(new GetPublicServicesResponse([]));
        }

        var eventTypes = await eventTypeRepository.GetPublicListByOwnerUnfilteredAsync(query.TenantId, profile.OwnerUserId, cancellationToken);

        var services = eventTypes.Select(eventType => new PublicServiceResponse(
                eventType.Id,
                eventType.Slug,
                eventType.Title,
                eventType.Description,
                eventType.DurationMinutes,
                eventType.Settings.Payment.Price,
                eventType.Settings.Payment.Currency,
                eventType.Settings.Payment.DepositAmount,
                eventType.ImageUrl
            )
        ).ToArray();

        return Result<GetPublicServicesResponse>.Success(new GetPublicServicesResponse(services));
    }
}
