using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;

namespace Main.Features.Scheduling.Shared;

[PublicAPI]
public sealed record SchedulingProfileResponse(string Handle, string DisplayName, string? AvatarUrl)
{
    public static SchedulingProfileResponse From(SchedulingProfile profile)
    {
        return new SchedulingProfileResponse(profile.Handle, profile.DisplayName, profile.AvatarUrl);
    }
}
