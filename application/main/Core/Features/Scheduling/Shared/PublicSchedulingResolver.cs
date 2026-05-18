using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Scheduling.Shared;

public sealed record PublicSchedulingContext(SchedulingProfile Profile, EventType EventType, Schedule Schedule);

public sealed class PublicSchedulingResolver(
    ISchedulingProfileRepository schedulingProfileRepository,
    IEventTypeRepository eventTypeRepository,
    IScheduleRepository scheduleRepository,
    TimeProvider timeProvider
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

    private bool CanAccess(EventType eventType, string? privateLink)
    {
        if (!eventType.Hidden) return true;
        if (string.IsNullOrWhiteSpace(privateLink)) return false;
        var now = timeProvider.GetUtcNow();
        return eventType.Settings.PrivateLinks.Any(link =>
            string.Equals(link.Link, privateLink.Trim(), StringComparison.OrdinalIgnoreCase) &&
            IsPrivateLinkActive(link, now)
        );
    }

    private static bool IsPrivateLinkActive(EventTypePrivateLink privateLink, DateTimeOffset now)
    {
        return (privateLink.ExpiresAt is null || privateLink.ExpiresAt > now) &&
               (privateLink.MaxUsageCount is null || privateLink.UsageCount < privateLink.MaxUsageCount);
    }
}
