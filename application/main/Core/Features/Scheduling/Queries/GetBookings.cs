using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Read)]
public sealed record GetBookingsQuery(
    string Status = "upcoming",
    string[]? Statuses = null,
    string? Search = null,
    EventTypeId? EventTypeId = null,
    string? AttendeeName = null,
    string? AttendeeEmail = null,
    BookingId? BookingUid = null,
    DateTimeOffset? AfterStartDate = null,
    DateTimeOffset? BeforeEndDate = null,
    bool NoShowOnly = false,
    int? MinRating = null,
    bool HasInternalNote = false,
    int PageOffset = 0,
    int PageSize = 25
) : IRequest<Result<BookingsResponse>>
{
    public string Status { get; } = Status.Trim().ToLowerInvariant();

    public string[] Statuses { get; } = NormalizeStatuses(Statuses, Status);

    public string? Search { get; } = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim().ToLowerInvariant();

    public string? AttendeeName { get; } = string.IsNullOrWhiteSpace(AttendeeName) ? null : AttendeeName.Trim().ToLowerInvariant();

    public string? AttendeeEmail { get; } = string.IsNullOrWhiteSpace(AttendeeEmail) ? null : AttendeeEmail.Trim().ToLowerInvariant();

    private static string[] NormalizeStatuses(string[]? statuses, string fallbackStatus)
    {
        var normalizedStatuses = statuses?
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

        return normalizedStatuses is { Length: > 0 } ? normalizedStatuses : [fallbackStatus.Trim().ToLowerInvariant()];
    }
}

public sealed class GetBookingsQueryValidator : AbstractValidator<GetBookingsQuery>
{
    private static readonly string[] ValidStatuses = ["upcoming", "recurring", "past", "cancelled", "unconfirmed"];

    public GetBookingsQueryValidator()
    {
        RuleFor(query => query.Status).Must(status => ValidStatuses.Contains(status)).WithMessage("Booking status must be upcoming, recurring, past, cancelled, or unconfirmed.");
        RuleForEach(query => query.Statuses).Must(status => ValidStatuses.Contains(status)).WithMessage("Booking status must be upcoming, recurring, past, cancelled, or unconfirmed.");
        RuleFor(query => query.Search).MaximumLength(120);
        RuleFor(query => query.AttendeeName).MaximumLength(120);
        RuleFor(query => query.AttendeeEmail).MaximumLength(320);
        RuleFor(query => query.PageOffset).GreaterThanOrEqualTo(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.MinRating!).InclusiveBetween(1, 5).When(query => query.MinRating is not null);
    }
}

[PublicAPI]
public sealed record BookingsResponse(int TotalCount, int PageOffset, int PageSize, BookingListItemResponse[] Bookings);

[PublicAPI]
public sealed record BookingListItemResponse(
    BookingId Id,
    string SchedulingHandle,
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
    string ListingStatus,
    string? LocationType,
    string? LocationValue,
    EventTypeLocation[] Locations,
    Dictionary<string, string> Responses,
    Dictionary<string, string> Metadata,
    BookingCalAttendee[] Attendees,
    BookingCalReference[] References,
    BookingSeatReference[] SeatReferences,
    string? CancellationReason,
    string? RejectionReason,
    string? RescheduleReason,
    bool Rescheduled,
    string? CancelledBy,
    string? RescheduledBy,
    bool IsRecurring,
    BookingActionsResponse Actions
);

public sealed class GetBookingsHandler(
    IBookingRepository bookingRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IBookingInternalNoteRepository bookingInternalNoteRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetBookingsQuery, Result<BookingsResponse>>
{
    public async Task<Result<BookingsResponse>> Handle(GetBookingsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingsResponse>.Unauthorized("Authentication is required.");
        }

        var now = timeProvider.GetUtcNow();
        var profile = await schedulingProfileRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        var bookings = await bookingRepository.GetForOwnerWithEventTypesAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        var preFiltered = bookings
            .Where(booking => query.Statuses.Any(status => MatchesStatus(booking, status, now)))
            .Where(booking => MatchesFilters(booking, query))
            .ToArray();

        var filtered = preFiltered;
        if (query.HasInternalNote)
        {
            var bookingIds = preFiltered.Select(item => item.Booking.Id).ToArray();
            var bookingsWithNotes = new HashSet<BookingId>();
            foreach (var bookingId in bookingIds)
            {
                var notes = await bookingInternalNoteRepository.GetForBookingAsync(bookingId, cancellationToken);
                if (notes.Length > 0) bookingsWithNotes.Add(bookingId);
            }

            filtered = preFiltered.Where(item => bookingsWithNotes.Contains(item.Booking.Id)).ToArray();
        }

        var ordered = OrderByStatus(filtered, query.Statuses);
        var page = ordered
            .Skip(query.PageOffset)
            .Take(query.PageSize)
            .Select(booking => ToResponse(booking, profile?.Handle ?? string.Empty, ResolveListingStatus(booking, query.Statuses, now), now))
            .ToArray();

        return new BookingsResponse(filtered.Length, query.PageOffset, query.PageSize, page);
    }

    private static bool MatchesStatus(BookingWithEventType item, string status, DateTimeOffset now)
    {
        var booking = item.Booking;
        var isCancelled = booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected;
        var isPending = booking.Status == BookingStatus.Pending;
        var isAccepted = booking.Status == BookingStatus.Accepted;
        var isRecurring = item.EventType.Settings.Recurrence is not null;

        return status switch
        {
            "upcoming" => booking.EndTime >= now && (!isRecurring || isAccepted) && !isCancelled,
            "recurring" => booking.EndTime >= now && isRecurring && !isCancelled,
            "past" => booking.EndTime <= now && !isCancelled,
            "cancelled" => isCancelled,
            "unconfirmed" => booking.EndTime >= now && isPending,
            _ => false
        };
    }

    private static bool MatchesFilters(BookingWithEventType item, GetBookingsQuery query)
    {
        var booking = item.Booking;
        var eventType = item.EventType;

        if (query.EventTypeId is not null && eventType.Id != query.EventTypeId) return false;
        if (query.BookingUid is not null && booking.Id != query.BookingUid) return false;
        if (query.AfterStartDate is not null && booking.StartTime < query.AfterStartDate) return false;
        if (query.BeforeEndDate is not null && booking.EndTime > query.BeforeEndDate) return false;
        if (query.AttendeeName is not null && !booking.BookerName.Contains(query.AttendeeName, StringComparison.OrdinalIgnoreCase)) return false;
        if (query.AttendeeEmail is not null && !booking.BookerEmail.Contains(query.AttendeeEmail, StringComparison.OrdinalIgnoreCase)) return false;
        if (query.NoShowOnly && booking.NoShowHost != true) return false;
        if (query.MinRating is not null && (booking.Rating is null || booking.Rating < query.MinRating)) return false;

        return query.Search is null ||
               eventType.Title.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
               eventType.Slug.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
               booking.BookerName.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
               booking.BookerEmail.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
               booking.Id.Value.Contains(query.Search, StringComparison.OrdinalIgnoreCase);
    }

    private static BookingWithEventType[] OrderByStatus(BookingWithEventType[] bookings, string[] statuses)
    {
        var ordered = statuses is ["past" or "cancelled"]
            ? bookings.OrderByDescending(booking => booking.Booking.StartTime).ThenBy(booking => booking.Booking.Id)
            : bookings.OrderBy(booking => booking.Booking.StartTime).ThenBy(booking => booking.Booking.Id);

        return ordered.ToArray();
    }

    private static string ResolveListingStatus(BookingWithEventType item, string[] statuses, DateTimeOffset now)
    {
        return statuses.First(status => MatchesStatus(item, status, now));
    }

    private static BookingListItemResponse ToResponse(BookingWithEventType item, string schedulingHandle, string listingStatus, DateTimeOffset now)
    {
        var booking = item.Booking;
        var eventType = item.EventType;
        return new BookingListItemResponse(
            booking.Id,
            schedulingHandle,
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
            listingStatus,
            booking.LocationType ?? eventType.LocationType,
            booking.LocationValue ?? eventType.LocationValue,
            eventType.Settings.Locations,
            JsonSerializer.Deserialize<Dictionary<string, string>>(booking.ResponsesJson) ?? [],
            booking.Metadata,
            booking.Attendees,
            booking.References,
            booking.SeatReferences,
            booking.CancellationReason,
            booking.RejectionReason,
            booking.RescheduleReason,
            booking.Rescheduled,
            booking.CancelledBy,
            booking.RescheduledBy,
            eventType.Settings.Recurrence is not null,
            BookingActionAvailability.Resolve(booking, eventType, now)
        );
    }
}
