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
        Status = string.Empty;
        ResponsesJson = "{}";
        MetadataJson = "{}";
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
        string status,
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
        Status = status.Trim();
        ResponsesJson = JsonSerializer.Serialize(responses, JsonSerializerOptions);
        MetadataJson = JsonSerializer.Serialize(metadata ?? new Dictionary<string, string>(StringComparer.Ordinal), JsonSerializerOptions);
        AttendeesJson = JsonSerializer.Serialize(
            new[] { new BookingAttendee(BookerName, BookerEmail, TimeZone, null, null, false) },
            JsonSerializerOptions
        );
        ReferencesJson = "[]";
        SeatReferencesJson = "[]";
        FromReschedule = null;
    }

    public UserId OwnerUserId { get; }

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
        CancellationReason = string.IsNullOrWhiteSpace(cancellationReason) ? null : cancellationReason.Trim();
        CancelledBy = string.IsNullOrWhiteSpace(cancelledBy) ? null : cancelledBy.Trim().ToLowerInvariant();
        RaiseSideEffectEvent(BookingSideEffectConstants.BookingCancelled);
    }

    public void RequestReschedule(string? rescheduleReason, string? rescheduledBy)
    {
        Status = "cancelled";
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
        string title,
        string? description,
        string? locationType,
        string? locationValue,
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
