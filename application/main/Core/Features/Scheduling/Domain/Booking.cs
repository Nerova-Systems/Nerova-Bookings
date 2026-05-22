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
        BookingStatus status,
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
        Status = status;
        ResponsesJson = JsonSerializer.Serialize(responses);
        ICalUid = $"{Id.Value}@nerova";
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

    public BookingStatus Status { get; private set; }

    public string ResponsesJson { get; private set; }

    /// <summary>
    ///     When non-null, references a Tenant of TenantKind.Team. When null, the aggregate is owned by the existing
    ///     user/solo scope.
    /// </summary>
    public TenantId? TeamId { get; private set; }

    // --- cal.com parity fields (state, audit, rating, iCal, recording, instant-meeting) ---

    public string? CancellationReason { get; private set; }

    public string? RejectionReason { get; private set; }

    public string? ReassignReason { get; private set; }

    public UserId? ReassignByUserId { get; private set; }

    public bool Rescheduled { get; private set; }

    public string? FromRescheduleUid { get; private set; }

    public string? CancelledByUserUid { get; private set; }

    public string? RescheduledByUserUid { get; private set; }

    public string? SmsReminderNumber { get; private set; }

    /// <summary>iCalendar UID. Set on creation and preserved across reschedules.</summary>
    public string? ICalUid { get; private set; }

    /// <summary>iCalendar sequence number. Incremented on each reschedule.</summary>
    public int ICalSequence { get; private set; }

    public int? Rating { get; private set; }

    public string? RatingFeedback { get; private set; }

    public bool? NoShowHost { get; private set; }

    public string? OneTimePassword { get; private set; }

    public bool IsRecorded { get; private set; }

    /// <summary>Cal.com <c>customInputs</c> legacy field responses as a JSON blob.</summary>
    public string? CustomInputsJson { get; private set; }

    /// <summary>Arbitrary metadata blob mirroring cal.com <c>metadata</c>.</summary>
    public string? MetadataJson { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public void AssignToTeam(TenantId teamId)
    {
        TeamId = teamId;
    }

    public void RemoveFromTeam()
    {
        TeamId = null;
    }

    public void Cancel(string? reason = null, string? cancelledByUserUid = null)
    {
        Status = BookingStatus.Cancelled;
        CancellationReason = reason?.Trim();
        CancelledByUserUid = cancelledByUserUid;
    }

    public void Confirm()
    {
        Status = BookingStatus.Accepted;
    }

    public void Reject(string reason)
    {
        Status = BookingStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public void MarkRescheduled(string? rescheduledByUserUid = null)
    {
        Rescheduled = true;
        RescheduledByUserUid = rescheduledByUserUid;
        ICalSequence += 1;
    }

    public void Reassign(UserId newOwnerUserId, string? reason = null, UserId? reassignByUserId = null)
    {
        OwnerUserId = newOwnerUserId;
        ReassignReason = reason?.Trim();
        ReassignByUserId = reassignByUserId;
    }

    public void Rate(int rating, string? feedback)
    {
        Rating = rating;
        RatingFeedback = feedback?.Trim();
    }

    public void SetNoShowHost(bool value)
    {
        NoShowHost = value;
    }

    public void MarkRecorded()
    {
        IsRecorded = true;
    }

    public void SetOneTimePassword(string oneTimePassword)
    {
        OneTimePassword = oneTimePassword;
    }

    public void SetSmsReminderNumber(string? number)
    {
        SmsReminderNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
    }

    public void SetCustomInputsJson(string? customInputsJson)
    {
        CustomInputsJson = customInputsJson;
    }

    public void SetMetadataJson(string? metadataJson)
    {
        MetadataJson = metadataJson;
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
        BookingStatus status,
        Dictionary<string, string> responses,
        TenantId? teamId = null
    )
    {
        var booking = new Booking(tenantId, ownerUserId, eventTypeId, startTime, startTime.AddMinutes(durationMinutes), beforeEventBufferMinutes, afterEventBufferMinutes, bookerName, bookerEmail, timeZone, status, responses);
        if (teamId is not null) booking.AssignToTeam(teamId);
        return booking;
    }
}
