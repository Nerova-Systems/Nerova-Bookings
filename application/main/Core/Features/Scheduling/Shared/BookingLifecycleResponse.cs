using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Queries;

namespace Main.Features.Scheduling.Shared;

[PublicAPI]
public sealed record BookingLifecycleResponse(
    BookingId Id,
    BookingStatus Status,
    BookingAttendeeResponse[] Attendees,
    string? LocationType,
    string? LocationValue
);
