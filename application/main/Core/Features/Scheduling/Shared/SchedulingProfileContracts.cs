using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Scheduling.Shared;

[PublicAPI]
public sealed record SchedulingProfileResponse(string Handle, string DisplayName, string? AvatarUrl, NerovaVertical? Vertical)
{
    public static SchedulingProfileResponse From(SchedulingProfile profile)
    {
        return new SchedulingProfileResponse(profile.Handle, profile.DisplayName, profile.AvatarUrl, profile.Vertical);
    }
}
