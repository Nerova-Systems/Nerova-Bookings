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

    /// <summary>
    ///     When non-null, references a Tenant of TenantKind.Team. When null, the aggregate is owned by the existing
    ///     user/solo scope.
    /// </summary>
    public TenantId? TeamId { get; private set; }

    public TenantId TenantId { get; } = new(0);

    /// <summary>
    ///     Assigns this schedule to a team.
    /// </summary>
    /// <remarks>
    ///     The command layer is responsible for verifying that <paramref name="teamId" /> references a Tenant of
    ///     TenantKind.Team. This aggregate cannot verify TenantKind itself.
    /// </remarks>
    public void AssignToTeam(TenantId teamId)
    {
        // Command layer must ensure teamId refers to a TenantKind.Team tenant.
        TeamId = teamId;
    }

    /// <summary>
    ///     Removes the team association, reverting the schedule to user/solo scope.
    /// </summary>
    public void RemoveFromTeam()
    {
        TeamId = null;
    }

    public static Schedule Create(
        TenantId tenantId,
        UserId ownerUserId,
        string name,
        string timeZone,
        bool isDefault,
        AvailabilityWindow[] availabilityWindows,
        AvailabilityDateOverride[] dateOverrides,
        TenantId? teamId = null
    )
    {
        var schedule = new Schedule(tenantId, ownerUserId, name, timeZone, isDefault, availabilityWindows, dateOverrides);
        if (teamId is not null) schedule.AssignToTeam(teamId);
        return schedule;
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
