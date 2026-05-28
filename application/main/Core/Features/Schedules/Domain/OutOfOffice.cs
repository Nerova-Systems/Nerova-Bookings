using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Schedules.Domain;

[IdPrefix("ooo")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, OutOfOfficeId>))]
public sealed record OutOfOfficeId(string Value) : StronglyTypedUlid<OutOfOfficeId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Represents a date range during which a user is out of office.
///     Mirrors cal.com's <c>OutOfOfficeEntry</c> model. Bookings cannot be offered for
///     dates inside <see cref="StartDate" />..<see cref="EndDate" /> (inclusive). If
///     <see cref="ToUserId" /> is set, bookings should be forwarded to that user
///     (forwarding is not implemented by slot calculation itself, only deemed-OOO is).
/// </summary>
public sealed class OutOfOffice : AggregateRoot<OutOfOfficeId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private OutOfOffice() : base(OutOfOfficeId.NewId())
    {
        UserId = new UserId(string.Empty);
    }

    private OutOfOffice(TenantId tenantId, UserId userId, DateOnly startDate, DateOnly endDate, UserId? toUserId, string? reason, string? notes)
        : base(OutOfOfficeId.NewId())
    {
        TenantId = tenantId;
        UserId = userId;
        StartDate = startDate;
        EndDate = endDate;
        ToUserId = toUserId;
        Reason = reason?.Trim();
        Notes = notes?.Trim();
    }

    public UserId UserId { get; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public UserId? ToUserId { get; private set; }

    public string? Reason { get; private set; }

    public string? Notes { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static OutOfOffice Create(TenantId tenantId, UserId userId, DateOnly startDate, DateOnly endDate, UserId? toUserId = null, string? reason = null, string? notes = null)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Out-of-office end date must be on or after start date.", nameof(endDate));
        }

        if (toUserId is not null && toUserId == userId)
        {
            throw new ArgumentException("Out-of-office cannot forward bookings to the same user.", nameof(toUserId));
        }

        return new OutOfOffice(tenantId, userId, startDate, endDate, toUserId, reason, notes);
    }

    public void Update(DateOnly startDate, DateOnly endDate, UserId? toUserId, string? reason, string? notes)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Out-of-office end date must be on or after start date.", nameof(endDate));
        }

        if (toUserId is not null && toUserId == UserId)
        {
            throw new ArgumentException("Out-of-office cannot forward bookings to the same user.", nameof(toUserId));
        }

        StartDate = startDate;
        EndDate = endDate;
        ToUserId = toUserId;
        Reason = reason?.Trim();
        Notes = notes?.Trim();
    }

    public bool Covers(DateOnly date)
    {
        return date >= StartDate && date <= EndDate;
    }
}
