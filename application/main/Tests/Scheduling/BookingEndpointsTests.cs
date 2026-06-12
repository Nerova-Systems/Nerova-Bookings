using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using static Main.Tests.DateDriftTestDates;

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
        var upcoming = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");
        var pending = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 8, 0), "Grace Hopper", "grace@example.com");
        var recurring = await CreateBookingAsync("weekly-sync", FutureDateTimeText(1, 7, 0), "Alan Turing", "alan@example.com");
        var past = await CreateBookingAsync("intro-call", FutureDateTimeText(2, 7, 0), "Katherine Johnson", "katherine@example.com");
        var cancelled = await CreateBookingAsync("intro-call", FutureDateTimeText(2, 8, 0), "Margaret Hamilton", "margaret@example.com");
        Connection.Update("bookings", "id", pending.Id, [("status", "Pending")]);
        Connection.Update("bookings", "id", past.Id, [("start_time", PastDateTime(7, 0)), ("end_time", PastDateTime(7, 30))]);
        Connection.Update("bookings", "id", cancelled.Id, [("status", "Cancelled")]);

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
        await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

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
        var upcoming = await CreateBookingAsync("intro-call", FutureDateTimeText(36, 7, 0), "Ada Lovelace", "ada@example.com");
        var past = await CreateBookingAsync("intro-call", FutureDateTimeText(43, 7, 0), "Katherine Johnson", "katherine@example.com");
        var outsideWindow = await CreateBookingAsync("intro-call", FutureDateTimeText(50, 7, 0), "Dorothy Vaughan", "dorothy@example.com");
        Connection.Update("bookings", "id", past.Id, [("start_time", FutureDateTime(32, 7, 0)), ("end_time", FutureDateTime(32, 7, 30))]);
        Connection.Update("bookings", "id", outsideWindow.Id, [("start_time", FutureDateTime(46, 7, 0)), ("end_time", FutureDateTime(46, 7, 30))]);

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(
            $"/api/bookings?statuses=upcoming&statuses=past&afterStartDate={FutureDateTimeText(30, 0, 0)}&beforeEndDate={FutureDateTimeText(36, 23, 59, 59)}&pageSize=100"
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
        var upcoming = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");
        var pending = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 8, 0), "Grace Hopper", "grace@example.com");
        var past = await CreateBookingAsync("intro-call", FutureDateTimeText(2, 7, 0), "Katherine Johnson", "katherine@example.com");
        var cancelled = await CreateBookingAsync("intro-call", FutureDateTimeText(2, 8, 0), "Margaret Hamilton", "margaret@example.com");
        var rejected = await CreateBookingAsync("intro-call", FutureDateTimeText(2, 9, 0), "Dorothy Vaughan", "dorothy@example.com");
        Connection.Update("bookings", "id", pending.Id, [("status", "Pending")]);
        Connection.Update("bookings", "id", past.Id, [("start_time", PastDateTime(7, 0)), ("end_time", PastDateTime(7, 30))]);
        Connection.Update("bookings", "id", cancelled.Id, [("status", "Cancelled")]);
        Connection.Update("bookings", "id", rejected.Id, [("status", "Rejected")]);

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
        actionsByBookingId[upcoming.Id].Reschedule.Enabled.Should().BeTrue();
        actionsByBookingId[upcoming.Id].RequestReschedule.Enabled.Should().BeTrue();
        actionsByBookingId[upcoming.Id].AddGuests.Enabled.Should().BeTrue();
        actionsByBookingId[upcoming.Id].Report.Enabled.Should().BeTrue();
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
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelBooking_WhenBookingCannotBeCancelled_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var past = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");
        Connection.Update("bookings", "id", past.Id, [("start_time", PastDateTime(7, 0)), ("end_time", PastDateTime(7, 30))]);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{past.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = past.Id }]).Should().Be("Accepted");
    }

    [Fact]
    public async Task CancelBooking_WhenEventTypeDisallowsCancellation_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", new { cancellationPolicy = new { allowCancellation = false } });
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Accepted");
    }

    [Fact]
    public async Task CancelBooking_WhenMemberCancelsOwnerBooking_ShouldReturnNotFound()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Accepted");
    }

    [Fact]
    public async Task CancelBooking_WhenOwnerCancelsMemberBooking_ShouldCancelBooking()
    {
        // Arrange — booking is created via Owner's handle then reassigned to Member directly in
        // the database, so cancellation by the Owner must go through the tenant-scoped lookup
        // (Admin/Owner can act on any booking in their tenant; Member is restricted to their own).
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");
        Connection.Update("bookings", "id", booking.Id, [("owner_user_id", DatabaseSeeder.Tenant1Member.Id!.ToString())]);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Cancelled");
    }

    [Fact]
    public async Task ReportBooking_WhenMemberReportsBooking_ShouldCreateReport()
    {
        // Arrange
        await UpdateSchedulingProfileAsync(AuthenticatedOwnerHttpClient, "owner", "Owner Name");
        var schedule = await CreateScheduleAsync(AuthenticatedOwnerHttpClient);
        await CreateEventTypeAsync(AuthenticatedOwnerHttpClient, schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("owner", "intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            $"/api/bookings/{booking.Id}/report",
            new { reasonCode = "Spam", notes = "Looks fishy" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM booking_reports WHERE booking_id = @id", [new { id = booking.Id }]).Should().Be(1);
    }

    [Fact]
    public async Task GetReports_WhenMember_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/bookings/reports");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetReports_WhenOwner_ShouldReturnTenantReports()
    {
        // Arrange
        await UpdateSchedulingProfileAsync(AuthenticatedOwnerHttpClient, "owner", "Owner Name");
        var schedule = await CreateScheduleAsync(AuthenticatedOwnerHttpClient);
        await CreateEventTypeAsync(AuthenticatedOwnerHttpClient, schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("owner", "intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");
        await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            $"/api/bookings/{booking.Id}/report",
            new { reasonCode = "Abuse", notes = (string?)null }
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/bookings/reports");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var reports = await response.DeserializeResponse<BookingReportsListResponse>();
        reports!.TotalCount.Should().Be(1);
        reports.Reports.Should().HaveCount(1);
        reports.Reports[0].BookingId.Should().Be(booking.Id);
        reports.Reports[0].ReasonCode.Should().Be("Abuse");
    }

    [Fact]
    public async Task ConfirmBooking_WhenBookingIsPending_ShouldAcceptBooking()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", new { confirmationPolicy = new { requiresConfirmation = true } });
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/confirm", null);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var lifecycle = await response.DeserializeResponse<BookingLifecycleResponse>();
        lifecycle!.Status.Should().Be("Accepted");
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Accepted");
    }

    [Fact]
    public async Task RejectBooking_WhenBookingIsPending_ShouldRejectBookingWithReason()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", new { confirmationPolicy = new { requiresConfirmation = true } });
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/bookings/{booking.Id}/reject", new { rejectionReason = "Not a fit" });

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var lifecycle = await response.DeserializeResponse<BookingLifecycleResponse>();
        lifecycle!.Status.Should().Be("Rejected");
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Rejected");
        Connection.ExecuteScalar<string>("SELECT rejection_reason FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Not a fit");
    }

    [Fact]
    public async Task RequestReschedule_WhenBookingIsAccepted_ShouldCancelAndRecordRequester()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/bookings/{booking.Id}/request-reschedule", new { rescheduleReason = "Need another time" });

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Cancelled");
        Connection.ExecuteScalar<long>("SELECT rescheduled FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT reschedule_reason FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("Need another time");
        Connection.ExecuteScalar<string>("SELECT cancelled_by FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("owner@tenant-1.com");
    }

    [Fact]
    public async Task AddBookingGuests_WhenGuestsAreValid_ShouldPersistAttendees()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/bookings/{booking.Id}/guests",
            new { guests = new[] { new { name = "Grace Hopper", email = "Grace@Example.com", timeZone = "UTC" } } }
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var lifecycle = await response.DeserializeResponse<BookingLifecycleResponse>();
        lifecycle!.Attendees.Select(attendee => attendee.Email).Should().Equal("ada@example.com", "grace@example.com");
    }

    [Fact]
    public async Task EditBookingLocation_WhenBookingIsAccepted_ShouldPersistLocationOverride()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/bookings/{booking.Id}/location", new { locationType = "phone", locationValue = "+27110000000" });

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var lifecycle = await response.DeserializeResponse<BookingLifecycleResponse>();
        lifecycle!.LocationType.Should().Be("phone");
        lifecycle.LocationValue.Should().Be("+27110000000");
        Connection.ExecuteScalar<string>("SELECT location_type FROM bookings WHERE id = @id", [new { id = booking.Id }]).Should().Be("phone");
    }

    [Fact]
    public async Task GetPublicRescheduleBooking_WhenBookingMatchesPublicEvent_ShouldReturnLimitedBookingData()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AnonymousHttpClient.GetAsync($"/api/public/reschedule-bookings/{booking.Id}?handle=owner&eventSlug=intro-call");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var rescheduleBooking = await response.DeserializeResponse<PublicRescheduleBookingResponse>();
        rescheduleBooking!.Id.Should().Be(booking.Id);
        rescheduleBooking.Handle.Should().Be("owner");
        rescheduleBooking.EventSlug.Should().Be("intro-call");
        rescheduleBooking.BookerName.Should().Be("Ada Lovelace");
        rescheduleBooking.BookerEmail.Should().Be("ada@example.com");
        rescheduleBooking.TimeZone.Should().Be("Africa/Johannesburg");
        rescheduleBooking.Responses.Should().ContainKey("topic").WhoseValue.Should().Be("Scheduling");
        rescheduleBooking.CanReschedule.Should().BeTrue();
    }

    [Fact]
    public async Task GetPublicRescheduleBooking_WhenBookingDoesNotMatchEventType_ShouldReturnNotFound()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateEventTypeAsync(schedule.Id, "Follow up", "follow-up");
        var booking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AnonymousHttpClient.GetAsync($"/api/public/reschedule-bookings/{booking.Id}?handle=owner&eventSlug=follow-up");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePublicBooking_WhenRescheduling_ShouldCreateReplacementAndMarkOriginalRescheduled()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var originalBooking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug = "intro-call",
                startTime = FutureDateTimeText(0, 8, 0),
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName = "Ada Lovelace",
                bookerEmail = "ada@example.com",
                responses = new Dictionary<string, string> { ["topic"] = "Moved" },
                rescheduleBookingId = originalBooking.Id,
                rescheduleReason = "Need a later time",
                rescheduledBy = "owner@tenant-1.com"
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        var replacement = await response.DeserializeResponse<CreatePublicBookingResponse>();
        replacement!.Id.Should().NotBe(originalBooking.Id);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = originalBooking.Id }]).Should().Be("Cancelled");
        Connection.ExecuteScalar<long>("SELECT rescheduled FROM bookings WHERE id = @id", [new { id = originalBooking.Id }]).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT reschedule_reason FROM bookings WHERE id = @id", [new { id = originalBooking.Id }]).Should().Be("Need a later time");
        Connection.ExecuteScalar<string>("SELECT rescheduled_by FROM bookings WHERE id = @id", [new { id = originalBooking.Id }]).Should().Be("owner@tenant-1.com");
        Connection.ExecuteScalar<string>("SELECT from_reschedule FROM bookings WHERE id = @id", [new { id = replacement.Id }]).Should().Be(originalBooking.Id);
    }

    [Fact]
    public async Task CreatePublicBooking_WhenRescheduleUsesUnavailableSlotExceptOriginal_ShouldAllowSameOriginalSlot()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var originalBooking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug = "intro-call",
                startTime = FutureDateTimeText(0, 7, 0),
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName = "Ada Lovelace",
                bookerEmail = "ada@example.com",
                responses = new Dictionary<string, string> { ["topic"] = "Same slot" },
                rescheduleBookingId = originalBooking.Id,
                rescheduleReason = "Keeping the same time",
                rescheduledBy = "owner@tenant-1.com"
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreatePublicBooking_WhenOriginalBookingCannotBeRescheduled_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", new { reschedulePolicy = new { allowReschedule = false } });
        var originalBooking = await CreateBookingAsync("intro-call", FutureDateTimeText(0, 7, 0), "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug = "intro-call",
                startTime = FutureDateTimeText(0, 8, 0),
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName = "Ada Lovelace",
                bookerEmail = "ada@example.com",
                responses = new Dictionary<string, string>(),
                rescheduleBookingId = originalBooking.Id,
                rescheduleReason = "Need another time",
                rescheduledBy = "owner@tenant-1.com"
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<string>("SELECT status FROM bookings WHERE id = @id", [new { id = originalBooking.Id }]).Should().Be("Accepted");
    }

    private async Task UpdateSchedulingProfileAsync(string handle)
    {
        await UpdateSchedulingProfileAsync(AuthenticatedOwnerHttpClient, handle, "Owner Name");
    }

    private static async Task UpdateSchedulingProfileAsync(HttpClient client, string handle, string displayName)
    {
        var response = await client.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName, avatarUrl = "https://example.com/avatar.png" }
        );
        response.EnsureSuccessStatusCode();
    }

    private Task<ScheduleResponse> CreateScheduleAsync()
    {
        return CreateScheduleAsync(AuthenticatedOwnerHttpClient);
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(HttpClient client)
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

        var response = await client.PostAsJsonAsync("/api/schedules", command);
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleResponse>())!;
    }

    private Task<EventTypeResponse> CreateEventTypeAsync(string scheduleId, string title, string slug, object? settings = null)
    {
        return CreateEventTypeAsync(AuthenticatedOwnerHttpClient, scheduleId, title, slug, settings);
    }

    private static async Task<EventTypeResponse> CreateEventTypeAsync(HttpClient client, string scheduleId, string title, string slug, object? settings = null)
    {
        var response = await client.PostAsJsonAsync(
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

    private Task<CreatePublicBookingResponse> CreateBookingAsync(string eventSlug, string startTime, string bookerName, string bookerEmail)
    {
        return CreateBookingAsync("owner", eventSlug, startTime, bookerName, bookerEmail);
    }

    private async Task<CreatePublicBookingResponse> CreateBookingAsync(string handle, string eventSlug, string startTime, string bookerName, string bookerEmail)
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle,
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
    private sealed record PublicRescheduleBookingResponse(
        string Id,
        string Handle,
        string EventSlug,
        string BookerName,
        string BookerEmail,
        string TimeZone,
        Dictionary<string, string> Responses,
        bool CanReschedule
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingLifecycleResponse(string Id, string Status, BookingAttendeeResponse[] Attendees, string? LocationType, string? LocationValue);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingAttendeeResponse(
        string Name,
        string Email,
        string TimeZone,
        string? PhoneNumber,
        string? Locale,
        bool NoShow
    );

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

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingReportsListResponse(int TotalCount, int PageOffset, int PageSize, BookingReportListItem[] Reports);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingReportListItem(
        string Id,
        string BookingId,
        string ReportedByUserId,
        string ReasonCode,
        string? Notes,
        DateTimeOffset CreatedAt
    );
}
