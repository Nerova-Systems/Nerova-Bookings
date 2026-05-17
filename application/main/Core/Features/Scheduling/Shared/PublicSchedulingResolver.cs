using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Scheduling.Shared;

public sealed record PublicSchedulingContext(SchedulingProfile Profile, EventType EventType, Schedule Schedule);

public sealed class PublicSchedulingResolver(
    ISchedulingProfileRepository schedulingProfileRepository,
    IEventTypeRepository eventTypeRepository,
    IScheduleRepository scheduleRepository
)
{
    public async Task<Result<PublicSchedulingContext>> ResolveAsync(string handle, string eventSlug, string? privateLink, CancellationToken cancellationToken)
    {
        var profile = await schedulingProfileRepository.GetByHandleUnfilteredAsync(handle, cancellationToken);
        if (profile is null)
        {
            return Result<PublicSchedulingContext>.NotFound($"Public event type '{handle}/{eventSlug}' was not found.");
        }

        var eventType = await eventTypeRepository.GetPublicBySlugUnfilteredAsync(profile.TenantId, profile.OwnerUserId, eventSlug, cancellationToken);
        if (eventType is null || !CanAccess(eventType, privateLink))
        {
            return Result<PublicSchedulingContext>.NotFound($"Public event type '{handle}/{eventSlug}' was not found.");
        }

        var schedule = await scheduleRepository.GetPublicByIdUnfilteredAsync(profile.TenantId, profile.OwnerUserId, eventType.ScheduleId, cancellationToken);
        if (schedule is null)
        {
            return Result<PublicSchedulingContext>.NotFound($"Public event type '{handle}/{eventSlug}' was not found.");
        }

        return new PublicSchedulingContext(profile, eventType, schedule);
    }

    private static bool CanAccess(EventType eventType, string? privateLink)
    {
        if (!eventType.Hidden) return true;
        if (string.IsNullOrWhiteSpace(privateLink)) return false;
        return eventType.Settings.PrivateLinks.Contains(privateLink.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
