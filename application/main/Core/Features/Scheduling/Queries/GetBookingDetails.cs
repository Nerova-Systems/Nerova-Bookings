using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
public sealed record GetBookingDetailsQuery(BookingId Id) : IRequest<Result<BookingDetailsResponse>>;

[PublicAPI]
public sealed record BookingDetailsResponse(
    BookingId Id,
    EventTypeId EventTypeId,
    string EventTypeTitle,
    string EventTypeSlug,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset CreatedAt,
    string BookerName,
    string BookerEmail,
    string TimeZone,
    BookingStatus Status,
    string? LocationType,
    string? LocationValue,
    EventTypeLocation[] Locations,
    Dictionary<string, string> Responses,
    bool IsRecurring,
    BookingActionsResponse Actions,
    string? CancellationReason,
    string? RejectionReason,
    string? ReassignReason,
    UserId? ReassignByUserId,
    bool Rescheduled,
    string? FromRescheduleUid,
    string? CancelledByUserUid,
    string? RescheduledByUserUid,
    string? SmsReminderNumber,
    string? ICalUid,
    int ICalSequence,
    int? Rating,
    string? RatingFeedback,
    bool? NoShowHost,
    bool IsRecorded,
    string? CustomInputsJson,
    string? MetadataJson,
    BookingAttendeeResponse[] Attendees,
    BookingSeatResponse[] Seats,
    BookingHistoryEntryResponse[] History,
    BookingInternalNoteResponse[] InternalNotes
);

[PublicAPI]
public sealed record BookingAttendeeResponse(BookingAttendeeId Id, string Name, string Email, string TimeZone, string Locale, bool NoShow);

[PublicAPI]
public sealed record BookingSeatResponse(BookingSeatId Id, BookingAttendeeId AttendeeId, string ReferenceUid);

[PublicAPI]
public sealed record BookingHistoryEntryResponse(BookingHistoryEntryId Id, BookingHistoryEventType EventType, DateTimeOffset OccurredAt, UserId? ActorUserId, string? PayloadJson);

[PublicAPI]
public sealed record BookingInternalNoteResponse(BookingInternalNoteId Id, UserId AuthorUserId, string Body, DateTimeOffset CreatedAt);

public sealed class GetBookingDetailsHandler(
    IBookingRepository bookingRepository,
    IBookingAttendeeRepository bookingAttendeeRepository,
    IBookingSeatRepository bookingSeatRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IBookingInternalNoteRepository bookingInternalNoteRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<GetBookingDetailsQuery, Result<BookingDetailsResponse>>
{
    public async Task<Result<BookingDetailsResponse>> Handle(GetBookingDetailsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingDetailsResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, query.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingDetailsResponse>.NotFound($"Booking '{query.Id}' was not found.");
        }

        var attendees = await bookingAttendeeRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var seats = await bookingSeatRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var history = await bookingHistoryEntryRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);
        var notes = await bookingInternalNoteRepository.GetForBookingAsync(item.Booking.Id, cancellationToken);

        var booking = item.Booking;
        var eventType = item.EventType;
        var response = new BookingDetailsResponse(
            booking.Id,
            eventType.Id,
            eventType.Title,
            eventType.Slug,
            eventType.Description,
            booking.StartTime,
            booking.EndTime,
            booking.CreatedAt,
            booking.BookerName,
            booking.BookerEmail,
            booking.TimeZone,
            booking.Status,
            booking.LocationType ?? eventType.LocationType,
            booking.LocationValue ?? eventType.LocationValue,
            eventType.Settings.Locations,
            JsonSerializer.Deserialize<Dictionary<string, string>>(booking.ResponsesJson) ?? [],
            eventType.Settings.Recurrence is not null,
            BookingActionAvailability.Resolve(booking, eventType, timeProvider.GetUtcNow()),
            booking.CancellationReason,
            booking.RejectionReason,
            booking.ReassignReason,
            booking.ReassignByUserId,
            booking.Rescheduled,
            booking.FromRescheduleUid,
            booking.CancelledByUserUid,
            booking.RescheduledByUserUid,
            booking.SmsReminderNumber,
            booking.ICalUid,
            booking.ICalSequence,
            booking.Rating,
            booking.RatingFeedback,
            booking.NoShowHost,
            booking.IsRecorded,
            booking.CustomInputsJson,
            booking.MetadataJson,
            attendees.Select(a => new BookingAttendeeResponse(a.Id, a.Name, a.Email, a.TimeZone, a.Locale, a.NoShow)).ToArray(),
            seats.Select(s => new BookingSeatResponse(s.Id, s.AttendeeId, s.ReferenceUid)).ToArray(),
            history.Select(h => new BookingHistoryEntryResponse(h.Id, h.EventType, h.OccurredAt, h.ActorUserId, h.PayloadJson)).ToArray(),
            notes.Select(n => new BookingInternalNoteResponse(n.Id, n.AuthorUserId, n.Body, n.CreatedAt)).ToArray()
        );

        return response;
    }
}
