using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.BookingSideEffects.Domain;
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
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    [UsedImplicitly]
    private Booking() : base(BookingId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
        Title = string.Empty;
        BookerName = string.Empty;
        BookerEmail = string.Empty;
        TimeZone = string.Empty;
        ResponsesJson = "{}";
        AttendeesJson = "[]";
        ReferencesJson = "[]";
        SeatReferencesJson = "[]";
        FromReschedule = null;
    }

    private Booking(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        string title,
        string? description,
        string? locationType,
        string? locationValue,
        string bookerName,
        string bookerEmail,
        string timeZone,
        BookingStatus status,
        Dictionary<string, string> responses,
        Dictionary<string, string>? metadata
    ) : base(BookingId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        EventTypeId = eventTypeId;
        StartTime = startTime;
        EndTime = endTime;
        BeforeEventBufferMinutes = beforeEventBufferMinutes;
        AfterEventBufferMinutes = afterEventBufferMinutes;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        LocationType = string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim();
        LocationValue = string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim();
        BookerName = bookerName.Trim();
        BookerEmail = bookerEmail.Trim().ToLowerInvariant();
        TimeZone = timeZone.Trim();
        Status = status;
        ResponsesJson = JsonSerializer.Serialize(responses, JsonSerializerOptions);
        MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonSerializerOptions);
        AttendeesJson = JsonSerializer.Serialize(
            new[] { new BookingAttendee(BookerName, BookerEmail, TimeZone, null, null, false) },
            JsonSerializerOptions
        );
        ReferencesJson = "[]";
        SeatReferencesJson = "[]";
        CalUid = $"{Id.Value}@nerova";
        FromReschedule = null;
    }

    public UserId OwnerUserId { get; private set; }

    public EventTypeId EventTypeId { get; }

    public DateTimeOffset StartTime { get; }

    public DateTimeOffset EndTime { get; }

    public int BeforeEventBufferMinutes { get; private set; }

    public int AfterEventBufferMinutes { get; private set; }

    public string Title { get; }

    public string? Description { get; private set; }

    public string? LocationType { get; private set; }

    public string? LocationValue { get; private set; }

    public string BookerName { get; }

    public string BookerEmail { get; }

    public string TimeZone { get; }

    public BookingStatus Status { get; private set; }

    public string ResponsesJson { get; private set; }


    public string AttendeesJson { get; private set; }

    public string ReferencesJson { get; private set; }

    public string SeatReferencesJson { get; }

    public string? CancellationReason { get; private set; }

    public string? RejectionReason { get; private set; }

    public string? RescheduleReason { get; private set; }

    public bool Rescheduled { get; private set; }

    public string? FromReschedule { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? RescheduledBy { get; private set; }

    [NotMapped]
    public BookingAttendee[] Attendees => JsonSerializer.Deserialize<BookingAttendee[]>(AttendeesJson, JsonSerializerOptions) ?? [];

    [NotMapped]
    public BookingReference[] References => JsonSerializer.Deserialize<BookingReference[]>(ReferencesJson, JsonSerializerOptions) ?? [];

    [NotMapped]
    public BookingSeatReference[] SeatReferences => JsonSerializer.Deserialize<BookingSeatReference[]>(SeatReferencesJson, JsonSerializerOptions) ?? [];

    [NotMapped]
    public Dictionary<string, string> Metadata => string.IsNullOrEmpty(MetadataJson) ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson, JsonSerializerOptions) ?? [];

    /// <summary>
    ///     When non-null, references a Tenant of TenantKind.Team. When null, the aggregate is owned by the existing
    ///     user/solo scope.
    /// </summary>
    public TenantId? TeamId { get; private set; }

    // --- cal.com parity fields (state, audit, rating, iCal, recording, instant-meeting) ---

    public string? ReassignReason { get; private set; }

    public UserId? ReassignByUserId { get; private set; }

    [UsedImplicitly]
    public string? FromRescheduleUid { get; init; }

    public string? CancelledByUserUid { get; private set; }

    public string? RescheduledByUserUid { get; private set; }

    public string? SmsReminderNumber { get; private set; }

    /// <summary>iCalendar UID. Set on creation and preserved across reschedules.</summary>
    public string? CalUid { get; private set; }

    /// <summary>iCalendar sequence number. Incremented on each reschedule.</summary>
    public int CalSequence { get; private set; }

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

    public void RecordCreated()
    {
        RaiseSideEffectEvent(BookingSideEffectConstants.BookingCreated);
    }

    public void RequestReschedule(string? rescheduleReason, string? rescheduledBy)
    {
        Status = BookingStatus.Cancelled;
        Rescheduled = true;
        RescheduleReason = string.IsNullOrWhiteSpace(rescheduleReason) ? null : rescheduleReason.Trim();
        CancellationReason = RescheduleReason;
        RescheduledBy = string.IsNullOrWhiteSpace(rescheduledBy) ? null : rescheduledBy.Trim().ToLowerInvariant();
        CancelledBy = RescheduledBy;
        RaiseSideEffectEvent(BookingSideEffectConstants.BookingRescheduled);
    }

    public void MarkAsReplacementFor(BookingId originalBookingId)
    {
        FromReschedule = originalBookingId.Value;
    }

    public void EditLocation(string? locationType, string? locationValue)
    {
        LocationType = string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim();
        LocationValue = string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim();
        RaiseSideEffectEvent(BookingSideEffectConstants.BookingLocationChanged);
    }

    public void AddGuests(BookingAttendee[] guests)
    {
        var attendees = Attendees.ToList();
        foreach (var guest in guests)
        {
            var normalizedEmail = guest.Email.Trim().ToLowerInvariant();
            if (string.Equals(normalizedEmail, BookerEmail, StringComparison.OrdinalIgnoreCase)) continue;
            if (attendees.Any(attendee => string.Equals(attendee.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))) continue;
            attendees.Add(guest with
                {
                    Name = guest.Name.Trim(),
                    Email = normalizedEmail,
                    TimeZone = string.IsNullOrWhiteSpace(guest.TimeZone) ? TimeZone : guest.TimeZone.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(guest.PhoneNumber) ? null : guest.PhoneNumber.Trim(),
                    Locale = string.IsNullOrWhiteSpace(guest.Locale) ? null : guest.Locale.Trim()
                }
            );
        }

        AttendeesJson = JsonSerializer.Serialize(attendees, JsonSerializerOptions);
        RaiseSideEffectEvent(BookingSideEffectConstants.BookingGuestsAdded);
    }

    public void UpsertReference(BookingReference reference)
    {
        var references = References
            .Where(existing => !IsSameReference(existing, reference))
            .Append(reference)
            .ToArray();

        ReferencesJson = JsonSerializer.Serialize(references, JsonSerializerOptions);
    }

    public void MarkReferencesDeleted(string type)
    {
        var references = References
            .Select(reference => reference.Type.Equals(type, StringComparison.OrdinalIgnoreCase) ? reference with { Deleted = true } : reference)
            .ToArray();

        ReferencesJson = JsonSerializer.Serialize(references, JsonSerializerOptions);
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
        CalSequence += 1;
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

    public void SetLocation(string? locationType, string? locationValue)
    {
        LocationType = string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim();
        LocationValue = string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim();
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
        return Create(tenantId, ownerUserId, eventTypeId, startTime, durationMinutes, beforeEventBufferMinutes, afterEventBufferMinutes, string.Empty, null, null, null, bookerName, bookerEmail, timeZone, status, responses, teamId);
    }

    public static Booking Create(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        DateTimeOffset startTime,
        int durationMinutes,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        string title,
        string? description,
        string? locationType,
        string? locationValue,
        string bookerName,
        string bookerEmail,
        string timeZone,
        BookingStatus status,
        Dictionary<string, string> responses,
        TenantId? teamId = null
    )
    {
        var booking = new Booking(tenantId, ownerUserId, eventTypeId, startTime, startTime.AddMinutes(durationMinutes), beforeEventBufferMinutes, afterEventBufferMinutes, title, description, locationType, locationValue, bookerName, bookerEmail, timeZone, status, responses, null);
        if (teamId is not null) booking.AssignToTeam(teamId);
        return booking;
    }

    private void RaiseSideEffectEvent(string trigger)
    {
        AddDomainEvent(
            new BookingLifecycleSideEffectEvent(
                TenantId,
                OwnerUserId,
                EventTypeId,
                Id,
                trigger,
                Title,
                BookerName,
                BookerEmail,
                StartTime,
                EndTime,
                Status,
                LocationType,
                LocationValue
            )
        );
    }

    private static bool IsSameReference(BookingReference existing, BookingReference replacement)
    {
        if (!existing.Type.Equals(replacement.Type, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(existing.ExternalCalendarId) || !string.IsNullOrWhiteSpace(replacement.ExternalCalendarId))
        {
            return string.Equals(existing.ExternalCalendarId, replacement.ExternalCalendarId, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(existing.Uid, replacement.Uid, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record BookingAttendee(
    string Name,
    string Email,
    string TimeZone,
    string? PhoneNumber,
    string? Locale,
    bool NoShow
);

public sealed record BookingReference(
    string Type,
    string Uid,
    string? MeetingId,
    string? MeetingPassword,
    string? MeetingUrl,
    string? ExternalCalendarId,
    bool Deleted
);

public sealed record BookingSeatReference(string Uid, string Name, string Email);
