using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Schedules.Domain;

[IdPrefix("trv")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, TravelScheduleId>))]
public sealed record TravelScheduleId(string Value) : StronglyTypedUlid<TravelScheduleId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Represents a temporary timezone override for a user while travelling.
///     Mirrors cal.com's <c>TravelSchedule</c> model. When the current date falls within
///     <see cref="StartDate" /> and <see cref="EndDate" /> (inclusive), slot calculation
///     uses <see cref="TimeZone" /> instead of the schedule's default timezone for that
///     date. Optionally <see cref="ScheduleId" /> can override which schedule applies
///     during the trip; when null the active schedule is used.
/// </summary>
public sealed class TravelSchedule : AggregateRoot<TravelScheduleId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private TravelSchedule() : base(TravelScheduleId.NewId())
    {
        UserId = new UserId(string.Empty);
        TimeZone = string.Empty;
    }

    private TravelSchedule(TenantId tenantId, UserId userId, DateOnly startDate, DateOnly endDate, string timeZone, ScheduleId? scheduleId)
        : base(TravelScheduleId.NewId())
    {
        TenantId = tenantId;
        UserId = userId;
        StartDate = startDate;
        EndDate = endDate;
        TimeZone = timeZone.Trim();
        ScheduleId = scheduleId;
    }

    public TenantId TenantId { get; } = new(0);

    public UserId UserId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string TimeZone { get; private set; }

    public ScheduleId? ScheduleId { get; private set; }

    public static TravelSchedule Create(TenantId tenantId, UserId userId, DateOnly startDate, DateOnly endDate, string timeZone, ScheduleId? scheduleId = null)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Travel schedule end date must be on or after start date.", nameof(endDate));
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new ArgumentException("Travel schedule time zone is required.", nameof(timeZone));
        }

        return new TravelSchedule(tenantId, userId, startDate, endDate, timeZone, scheduleId);
    }

    public void Update(DateOnly startDate, DateOnly endDate, string timeZone, ScheduleId? scheduleId)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Travel schedule end date must be on or after start date.", nameof(endDate));
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new ArgumentException("Travel schedule time zone is required.", nameof(timeZone));
        }

        StartDate = startDate;
        EndDate = endDate;
        TimeZone = timeZone.Trim();
        ScheduleId = scheduleId;
    }

    public bool Covers(DateOnly date)
    {
        return date >= StartDate && date <= EndDate;
    }
}
