using System.Collections.Immutable;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Schedules.Domain;

[IdPrefix("sch")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, ScheduleId>))]
public sealed record ScheduleId(string Value) : StronglyTypedUlid<ScheduleId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class Schedule : SoftDeletableAggregateRoot<ScheduleId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Schedule() : base(ScheduleId.NewId())
    {
        Name = string.Empty;
        TimeZone = string.Empty;
        OwnerUserId = new UserId(string.Empty);
        AvailabilityWindows = [];
        DateOverrides = [];
    }

    private Schedule(
        TenantId tenantId,
        UserId ownerUserId,
        string name,
        string timeZone,
        bool isDefault,
        AvailabilityWindow[] availabilityWindows,
        AvailabilityDateOverride[] dateOverrides
    )
        : base(ScheduleId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Name = name.Trim();
        TimeZone = timeZone.Trim();
        IsDefault = isDefault;
        AvailabilityWindows = [.. availabilityWindows.Select(window => window.Normalize())];
        DateOverrides = [.. dateOverrides.Select(dateOverride => dateOverride.Normalize()).OrderBy(dateOverride => dateOverride.Date)];
    }

    public UserId OwnerUserId { get; private set; }

    public string Name { get; private set; }

    public string TimeZone { get; private set; }

    public bool IsDefault { get; private set; }

    public ImmutableArray<AvailabilityWindow> AvailabilityWindows { get; private set; }

    public ImmutableArray<AvailabilityDateOverride> DateOverrides { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static Schedule Create(
        TenantId tenantId,
        UserId ownerUserId,
        string name,
        string timeZone,
        bool isDefault,
        AvailabilityWindow[] availabilityWindows,
        AvailabilityDateOverride[] dateOverrides
    )
    {
        return new Schedule(tenantId, ownerUserId, name, timeZone, isDefault, availabilityWindows, dateOverrides);
    }

    public void Update(string name, string timeZone, bool isDefault, AvailabilityWindow[] availabilityWindows, AvailabilityDateOverride[] dateOverrides)
    {
        Name = name.Trim();
        TimeZone = timeZone.Trim();
        IsDefault = isDefault;
        AvailabilityWindows = [.. availabilityWindows.Select(window => window.Normalize())];
        DateOverrides = [.. dateOverrides.Select(dateOverride => dateOverride.Normalize()).OrderBy(dateOverride => dateOverride.Date)];
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
