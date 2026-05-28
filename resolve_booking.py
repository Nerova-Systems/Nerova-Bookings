"""
Resolves Booking.cs conflicts and post-conflict cleanup.
Run from the repo root.
"""
import re, os

BASE = r"C:\Users\colin\dev\NerovaBookings"
f = os.path.join(BASE, r"application\main\Core\Features\Scheduling\Domain\Booking.cs")

with open(f, "r", encoding="utf-8") as fh:
    c = fh.read()

CONFLICT = re.compile(r"<<<<<<< HEAD\r?\n(.*?)\r?\n=======\r?\n(.*?)\r?\n>>>>>>> [0-9a-f]+[^\r\n]*", re.DOTALL)

matches = list(CONFLICT.finditer(c))
print(f"Conflicts found: {len(matches)}")
for i, m in enumerate(matches):
    print(f"  [{i}] head starts: {m.group(1)[:60]!r}")
    print(f"      merge starts: {m.group(2)[:60]!r}")

# ── Conflict 0: constructor params ──────────────────────────────────────────
# HEAD: string status, Dictionary<string,string> responses, Dictionary<string,string>? metadata
# MERGE: BookingStatus status, Dictionary<string,string> responses
# Resolution: BookingStatus status + keep metadata param (factory still passes null)
RESOLVED_PARAMS = "        BookingStatus status,\n        Dictionary<string, string> responses,\n        Dictionary<string, string>? metadata"

# ── Conflict 1: constructor body ─────────────────────────────────────────────
# Keep HEAD initializations but use enum Status, serialize metadata conditionally, add CalUid
RESOLVED_BODY = (
    "        Status = status;\n"
    "        ResponsesJson = JsonSerializer.Serialize(responses, JsonSerializerOptions);\n"
    "        MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonSerializerOptions);\n"
    "        AttendeesJson = JsonSerializer.Serialize(\n"
    "            new[] { new BookingAttendee(BookerName, BookerEmail, TimeZone, null, null, false) },\n"
    "            JsonSerializerOptions\n"
    "        );\n"
    "        ReferencesJson = \"[]\";\n"
    "        SeatReferencesJson = \"[]\";\n"
    "        CalUid = $\"{Id.Value}@nerova\";\n"
    "        FromReschedule = null;"
)

# ── Conflict 2: methods block ────────────────────────────────────────────────
# Keep HEAD unique methods (RecordCreated, RequestReschedule, MarkAsReplacementFor,
# EditLocation, AddGuests, UpsertReference, MarkReferencesDeleted) + MERGE's Cancel.
# Drop HEAD's Confirm, Reject, old Cancel (MERGE versions are in non-conflicting section).
RESOLVED_METHODS = r"""    public void RecordCreated()
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
        CancelledByUserUid = cancelledByUserUid;"""

resolutions = [RESOLVED_PARAMS, RESOLVED_BODY, RESOLVED_METHODS]
if len(matches) != 3:
    print(f"ERROR: expected 3 conflicts, got {len(matches)} -- aborting")
    exit(1)

result = c
for i in range(len(resolutions) - 1, -1, -1):
    m = list(CONFLICT.finditer(result))[i]
    result = result[:m.start()] + resolutions[i] + result[m.end():]

print(f"After conflict resolution, remaining markers: {len(CONFLICT.findall(result))}")

# ── Post-conflict cleanups ───────────────────────────────────────────────────

# 1) Remove HEAD's non-nullable MetadataJson (no private set); keep MERGE's nullable
result = result.replace(
    "\n    public string MetadataJson { get; }\n",
    "\n"
)

# 2) Remove duplicate CancellationReason from cal.com section
# The section has a comment "// --- cal.com parity fields" followed by the duplicate
# We identify it: after "// --- cal.com parity fields", the FIRST property is the duplicate CancellationReason
result = result.replace(
    "\n    // --- cal.com parity fields (state, audit, rating, iCal, recording, instant-meeting) ---\n\n    public string? CancellationReason { get; private set; }\n\n    public string? RejectionReason { get; private set; }\n",
    "\n    // --- cal.com parity fields (state, audit, rating, iCal, recording, instant-meeting) ---\n"
)

# 3) Remove duplicate Rescheduled from cal.com section
result = result.replace(
    "\n    public bool Rescheduled { get; private set; }\n\n    [UsedImplicitly]\n    public string? FromRescheduleUid",
    "\n    [UsedImplicitly]\n    public string? FromRescheduleUid"
)

# 4) Remove duplicate LocationType/LocationValue (the ones with XML doc comments in cal.com section)
# These are duplicates of the HEAD section's plain declarations
result = result.replace(
    '\n    /// <summary>Per-booking location-type override. When null, the booking inherits its event type\'s location.</summary>\n    public string? LocationType { get; private set; }\n\n    /// <summary>Per-booking location value override (URL, address, phone number, etc.).</summary>\n    public string? LocationValue { get; private set; }\n',
    '\n'
)

# 5) Fix Create factory: string status → BookingStatus status
result = result.replace(
    "        string status,\n        Dictionary<string, string> responses,\n        TenantId? teamId = null\n    )\n    {\n        var booking = new Booking(",
    "        BookingStatus status,\n        Dictionary<string, string> responses,\n        TenantId? teamId = null\n    )\n    {\n        var booking = new Booking("
)

# 6) Fix Metadata computed property to handle nullable MetadataJson
result = result.replace(
    "    public Dictionary<string, string> Metadata => JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson, JsonSerializerOptions) ?? [];",
    "    public Dictionary<string, string> Metadata => string.IsNullOrEmpty(MetadataJson) ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson, JsonSerializerOptions) ?? [];"
)

# 7) Remove MetadataJson = "{}" from private constructor since it's now nullable
result = result.replace(
    '        MetadataJson = "{}";\n',
    ''
)

# ── Verify no remaining conflict markers ────────────────────────────────────
remaining = len(CONFLICT.findall(result))
print(f"Remaining conflict markers: {remaining}")

with open(f, "w", encoding="utf-8") as fh:
    fh.write(result)

print("Booking.cs resolved!")

# Quick sanity: check for duplicate property declarations
prop_names = re.findall(r"    public \S+ (\w+) \{", result)
from collections import Counter
dupes = {k: v for k, v in Counter(prop_names).items() if v > 1}
if dupes:
    print(f"WARNING: Duplicate properties found: {dupes}")
else:
    print("No duplicate properties!")
