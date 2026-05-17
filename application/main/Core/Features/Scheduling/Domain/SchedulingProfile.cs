using System.Text.RegularExpressions;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("sprof")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, SchedulingProfileId>))]
public sealed record SchedulingProfileId(string Value) : StronglyTypedUlid<SchedulingProfileId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class SchedulingProfile : SoftDeletableAggregateRoot<SchedulingProfileId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private SchedulingProfile() : base(SchedulingProfileId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        Handle = string.Empty;
        DisplayName = string.Empty;
    }

    private SchedulingProfile(TenantId tenantId, UserId ownerUserId, string handle, string displayName, string? avatarUrl)
        : base(SchedulingProfileId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Update(handle, displayName, avatarUrl);
    }

    public UserId OwnerUserId { get; private set; }

    public string Handle { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? AvatarUrl { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static SchedulingProfile Create(TenantId tenantId, UserId ownerUserId, string handle, string displayName, string? avatarUrl)
    {
        return new SchedulingProfile(tenantId, ownerUserId, handle, displayName, avatarUrl);
    }

    public void Update(string handle, string displayName, string? avatarUrl)
    {
        Handle = NormalizeHandle(handle);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Handle : displayName.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
    }

    public static string NormalizeHandle(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-", RegexOptions.None, TimeSpan.FromSeconds(1));
        return normalized.Trim('-');
    }
}
