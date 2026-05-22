using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class BookingEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task GetBookings_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/bookings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookings_WhenOwnerHasBookings_ShouldReturnStatusViewsAndFilters()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var introEventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var recurringEventType = await CreateEventTypeAsync(schedule.Id, "Weekly sync", "weekly-sync", new { recurrence = new { frequency = "weekly", interval = 1, count = 4 } });
        var upcoming = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");
        var pending = await CreateBookingAsync("intro-call", "2026-06-01T08:00:00Z", "Grace Hopper", "grace@example.com");
        var recurring = await CreateBookingAsync("weekly-sync", "2026-06-02T07:00:00Z", "Alan Turing", "alan@example.com");
        var past = await CreateBookingAsync("intro-call", "2026-06-03T07:00:00Z", "Katherine Johnson", "katherine@example.com");
        var cancelled = await CreateBookingAsync("intro-call", "2026-06-03T08:00:00Z", "Margaret Hamilton", "margaret@example.com");
        Connection.Update("bookings", "id", pending.Id, [("status", "pending")]);
        Connection.Update("bookings", "id", past.Id, [("start_time", DateTimeOffset.Parse("2026-05-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2026-05-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", cancelled.Id, [("status", "cancelled")]);

        // Act
        var upcomingResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/bookings?status=upcoming&eventTypeId={introEventType.Id}&attendeeEmail=ada");
        var unconfirmedResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/bookings?status=unconfirmed");
        var recurringResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/bookings?status=recurring&eventTypeId={recurringEventType.Id}");
        var pastResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/bookings?status=past");
        var cancelledResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/bookings?status=cancelled");

        // Assert
        upcomingResponse.ShouldBeSuccessfulGetRequest();
        var upcomingBookings = await upcomingResponse.DeserializeResponse<BookingsResponse>();
        upcomingBookings!.Bookings.Select(booking => booking.Id).Should().Equal(upcoming.Id);
        upcomingBookings.Bookings[0].EventTypeTitle.Should().Be("Intro call");
        upcomingBookings.Bookings[0].BookerEmail.Should().Be("ada@example.com");

        unconfirmedResponse.ShouldBeSuccessfulGetRequest();
        var unconfirmedBookings = await unconfirmedResponse.DeserializeResponse<BookingsResponse>();
        unconfirmedBookings!.Bookings.Select(booking => booking.Id).Should().Equal(pending.Id);

        recurringResponse.ShouldBeSuccessfulGetRequest();
        var recurringBookings = await recurringResponse.DeserializeResponse<BookingsResponse>();
        recurringBookings!.Bookings.Select(booking => booking.Id).Should().Equal(recurring.Id);
        recurringBookings.Bookings[0].IsRecurring.Should().BeTrue();

        pastResponse.ShouldBeSuccessfulGetRequest();
        var pastBookings = await pastResponse.DeserializeResponse<BookingsResponse>();
        pastBookings!.Bookings.Select(booking => booking.Id).Should().Contain(past.Id);

        cancelledResponse.ShouldBeSuccessfulGetRequest();
        var cancelledBookings = await cancelledResponse.DeserializeResponse<BookingsResponse>();
        cancelledBookings!.Bookings.Select(booking => booking.Id).Should().Equal(cancelled.Id);
    }

    [Fact]
    public async Task GetBookings_WhenMemberRequestsOwnerBookings_ShouldReturnEmptyList()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/bookings?status=upcoming");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var bookings = await response.DeserializeResponse<BookingsResponse>();
        bookings!.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBookings_WhenCalendarQueryRequestsMultipleStatuses_ShouldReturnBookingsAcrossVisibleDateWindow()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var upcoming = await CreateBookingAsync("intro-call", "2026-07-07T07:00:00Z", "Ada Lovelace", "ada@example.com");
        var past = await CreateBookingAsync("intro-call", "2026-07-14T07:00:00Z", "Katherine Johnson", "katherine@example.com");
        var outsideWindow = await CreateBookingAsync("intro-call", "2026-07-21T07:00:00Z", "Dorothy Vaughan", "dorothy@example.com");
        Connection.Update("bookings", "id", past.Id, [("start_time", DateTimeOffset.Parse("2026-07-03T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2026-07-03T07:30:00Z"))]);
        Connection.Update("bookings", "id", outsideWindow.Id, [("start_time", DateTimeOffset.Parse("2026-07-17T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2026-07-17T07:30:00Z"))]);

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(
            "/api/bookings?statuses=upcoming&statuses=past&afterStartDate=2026-07-01T00:00:00Z&beforeEndDate=2026-07-07T23:59:59Z&pageSize=100"
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var bookings = await response.DeserializeResponse<BookingsResponse>();
        bookings!.Bookings.Select(booking => booking.Id).Should().Equal(past.Id, upcoming.Id);
    }

    [Fact]
    public async Task GetBookings_WhenBookingsHaveDifferentStates_ShouldReturnActionAvailability()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var upcoming = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");
        var pending = await CreateBookingAsync("intro-call", "2026-06-01T08:00:00Z", "Grace Hopper", "grace@example.com");
        var past = await CreateBookingAsync("intro-call", "2026-06-03T07:00:00Z", "Katherine Johnson", "katherine@example.com");
        var cancelled = await CreateBookingAsync("intro-call", "2026-06-03T08:00:00Z", "Margaret Hamilton", "margaret@example.com");
        var rejected = await CreateBookingAsync("intro-call", "2026-06-03T09:00:00Z", "Dorothy Vaughan", "dorothy@example.com");
        Connection.Update("bookings", "id", pending.Id, [("status", "pending")]);
        Connection.Update("bookings", "id", past.Id, [("start_time", DateTimeOffset.Parse("2026-05-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2026-05-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", cancelled.Id, [("status", "cancelled")]);
        Connection.Update("bookings", "id", rejected.Id, [("status", "rejected")]);

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(
            "/api/bookings?statuses=upcoming&statuses=unconfirmed&statuses=past&statuses=cancelled&pageSize=100"
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var bookings = await response.DeserializeResponse<BookingsResponse>();
        var actionsByBookingId = bookings!.Bookings.ToDictionary(booking => booking.Id, booking => booking.Actions);
        actionsByBookingId[upcoming.Id].Cancel.Should().Be(new BookingActionResponse(true, true, null));
        actionsByBookingId[pending.Id].Cancel.Should().Be(new BookingActionResponse(true, true, null));
        actionsByBookingId[past.Id].Cancel.Should().Be(new BookingActionResponse(true, false, "Past bookings cannot be cancelled."));
        actionsByBookingId[cancelled.Id].Cancel.Should().Be(new BookingActionResponse(true, false, "Cancelled bookings cannot be cancelled."));
        actionsByBookingId[rejected.Id].Cancel.Should().Be(new BookingActionResponse(true, false, "Rejected bookings cannot be cancelled."));
        actionsByBookingId[upcoming.Id].Reschedule.Enabled.Should().BeFalse();
        actionsByBookingId[upcoming.Id].AddGuests.Enabled.Should().BeFalse();
        actionsByBookingId[upcoming.Id].Report.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBooking_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/bookings/book_01HX0000000000000000000000/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelBooking_WhenOwnerCancelsFutureBooking_ShouldCancelBooking()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("cancelled");
    }

    [Fact]
    public async Task CancelBooking_WhenBookingCannotBeCancelled_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var past = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");
        Connection.Update("bookings", "id", past.Id, [("start_time", DateTimeOffset.Parse("2026-05-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2026-05-01T07:30:00Z"))]);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{past.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = past.Id }]).Should().Be("accepted");
    }

    [Fact]
    public async Task CancelBooking_WhenEventTypeDisallowsCancellation_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", new { cancellationPolicy = new { allowCancellation = false } });
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("accepted");
    }

    [Fact]
    public async Task CancelBooking_WhenMemberCancelsOwnerBooking_ShouldReturnNotFound()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("accepted");
    }

    private async Task UpdateSchedulingProfileAsync(string handle)
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = "https://example.com/avatar.png" }
        );
        response.EnsureSuccessStatusCode();
    }

    private async Task<ScheduleResponse> CreateScheduleAsync()
    {
        var command = new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = true,
            availabilityWindows = new[]
            {
                new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 }
            },
            dateOverrides = Array.Empty<object>()
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleResponse>())!;
    }

    private async Task<EventTypeResponse> CreateEventTypeAsync(string scheduleId, string title, string slug, object? settings = null)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types",
            new
            {
                title,
                slug,
                description = "A short consultation",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0,
                locationType = "link",
                locationValue = "https://example.com/meet",
                settings
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeResponse>())!;
    }

    private async Task<CreatePublicBookingResponse> CreateBookingAsync(string eventSlug, string startTime, string bookerName, string bookerEmail)
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug,
                startTime,
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName,
                bookerEmail,
                responses = new Dictionary<string, string> { ["topic"] = "Scheduling" }
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<CreatePublicBookingResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CreatePublicBookingResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingsResponse(int TotalCount, BookingResponse[] Bookings);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingResponse(string Id, string EventTypeTitle, string BookerEmail, bool IsRecurring, BookingActionsResponse Actions);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingActionsResponse(
        BookingActionResponse Cancel,
        BookingActionResponse Reschedule,
        BookingActionResponse RequestReschedule,
        BookingActionResponse EditLocation,
        BookingActionResponse AddGuests,
        BookingActionResponse ViewRecordings,
        BookingActionResponse ViewSessionDetails,
        BookingActionResponse MarkNoShow,
        BookingActionResponse Report
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingActionResponse(bool Visible, bool Enabled, string? DisabledReason);
}
