using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("book")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingId>))]
public sealed record BookingId(string Value) : StronglyTypedUlid<BookingId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class Booking : AggregateRoot<BookingId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Booking() : base(BookingId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
        BookerName = string.Empty;
        BookerEmail = string.Empty;
        TimeZone = string.Empty;
        Status = string.Empty;
        ResponsesJson = "{}";
    }

    private Booking(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        string bookerName,
        string bookerEmail,
        string timeZone,
        string status,
        Dictionary<string, string> responses
    ) : base(BookingId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        EventTypeId = eventTypeId;
        StartTime = startTime;
        EndTime = endTime;
        BeforeEventBufferMinutes = beforeEventBufferMinutes;
        AfterEventBufferMinutes = afterEventBufferMinutes;
        BookerName = bookerName.Trim();
        BookerEmail = bookerEmail.Trim().ToLowerInvariant();
        TimeZone = timeZone.Trim();
        Status = status.Trim();
        ResponsesJson = JsonSerializer.Serialize(responses);
    }

    public UserId OwnerUserId { get; private set; }

    public EventTypeId EventTypeId { get; private set; }

    public DateTimeOffset StartTime { get; private set; }

    public DateTimeOffset EndTime { get; private set; }

    public int BeforeEventBufferMinutes { get; private set; }

    public int AfterEventBufferMinutes { get; private set; }

    public string BookerName { get; private set; }

    public string BookerEmail { get; private set; }

    public string TimeZone { get; private set; }

    public string Status { get; private set; }

    public string ResponsesJson { get; private set; }

    /// <summary>
    ///     When non-null, references a Tenant of TenantKind.Team. When null, the aggregate is owned by the existing
    ///     user/solo scope.
    /// </summary>
    public TenantId? TeamId { get; private set; }

    public TenantId TenantId { get; } = new(0);

    /// <summary>
    ///     Assigns this booking to a team.
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
    ///     Removes the team association, reverting the booking to user/solo scope.
    /// </summary>
    public void RemoveFromTeam()
    {
        TeamId = null;
    }

    public void Cancel()
    {
        Status = "cancelled";
    }

    /// <summary>Reassigns this booking to a different host (round-robin reassignment).</summary>
    public void Reassign(UserId newOwnerUserId)
    {
        OwnerUserId = newOwnerUserId;
    }

    public static Booking Create(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        DateTimeOffset startTime,
        int durationMinutes,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        string bookerName,
        string bookerEmail,
        string timeZone,
        string status,
        Dictionary<string, string> responses,
        TenantId? teamId = null
    )
    {
        var booking = new Booking(tenantId, ownerUserId, eventTypeId, startTime, startTime.AddMinutes(durationMinutes), beforeEventBufferMinutes, afterEventBufferMinutes, bookerName, bookerEmail, timeZone, status, responses);
        if (teamId is not null) booking.AssignToTeam(teamId);
        return booking;
    }
}
