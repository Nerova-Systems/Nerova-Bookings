using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.RoundRobin;

/// <summary>
///     Integration tests for round-robin scheduling through the public API.
///     Verifies slot availability, host selection, and booking assignment.
/// </summary>
public sealed class RoundRobinBookingFlowTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _rrClient;

    // Fixed test date: Wednesday 2026-06-03 (mid-week, avoids weekend edge cases)
    // 09:00 SAST = 07:00 UTC; 10:00 SAST = 08:00 UTC
    private static readonly string TestDate = "2026-06-03";
    private static readonly string SlotTimeUtc = $"{TestDate}T07:00:00Z"; // 09:00 SAST
    private static readonly string FreeSlotTimeUtc = $"{TestDate}T08:00:00Z"; // 10:00 SAST

    public RoundRobinBookingFlowTests()
    {
        var ownerWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            ActiveTeamId = DatabaseSeeder.TenantId,
            FeatureFlags = new HashSet<string> { RoundRobinAuthorization.RoundRobinFeatureFlagKey }
        };
        _rrClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    // ─── Slots offered when all rotating hosts free ──────────────────────────

    [Fact]
    public async Task GetPublicSlots_WhenRoundRobinEventTypeWithAllHostsFree_ShouldOfferSlots()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeAsync();

        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle={handle}&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots.Should().ContainKey(TestDate);
        slots.Slots[TestDate].Should().Contain(s => s.Time == DateTimeOffset.Parse(SlotTimeUtc));
    }

    // ─── Slot still available when only one rotating host is busy ────────────

    [Fact]
    public async Task GetPublicSlots_WhenOneRotatingHostBusy_OtherFree_ShouldStillOfferSlot()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeAsync(addSecondRotatingHost: true);

        // Seed a conflicting booking only for the member (first rotating host)
        await SeedHostBookingAsync(DatabaseSeeder.Tenant1Member.Id!, eventType.Id, DateTimeOffset.Parse(SlotTimeUtc), 30);

        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle={handle}&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        // Slot should still appear because the second rotating host is free
        slots!.Slots[TestDate].Should().Contain(s => s.Time == DateTimeOffset.Parse(SlotTimeUtc));
    }

    // ─── Slot blocked when all rotating hosts are busy ───────────────────────

    [Fact]
    public async Task GetPublicSlots_WhenAllRotatingHostsBusy_ShouldExcludeSlot()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeAsync();

        // Seed a conflicting booking for the only rotating host
        await SeedHostBookingAsync(DatabaseSeeder.Tenant1Member.Id!, eventType.Id, DateTimeOffset.Parse(SlotTimeUtc), 30);

        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle={handle}&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        var allSlots = slots!.Slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().NotContain(s => s.Time == DateTimeOffset.Parse(SlotTimeUtc));
        allSlots.Should().Contain(s => s.Time == DateTimeOffset.Parse(FreeSlotTimeUtc));
    }

    // ─── Slot blocked when fixed host is busy ────────────────────────────────

    [Fact]
    public async Task GetPublicSlots_WhenFixedHostBusy_ShouldExcludeSlotEvenWithFreeRotatingHost()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeWithFixedHostAsync();

        // The owner is the fixed host — seed a booking for them
        await SeedHostBookingAsync(DatabaseSeeder.Tenant1Owner.Id!, eventType.Id, DateTimeOffset.Parse(SlotTimeUtc), 30);

        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle={handle}&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        var allSlots = slots!.Slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().NotContain(s => s.Time == DateTimeOffset.Parse(SlotTimeUtc));
    }

    // ─── Booking succeeds and is assigned to the rotating host ───────────────

    [Fact]
    public async Task CreatePublicBooking_WhenRoundRobinSlotAvailable_ShouldSucceedAndAssignRotatingHost()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeAsync();

        var command = new
        {
            handle,
            eventSlug = eventType.Slug,
            startTime = FreeSlotTimeUtc,
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Test Booker",
            bookerEmail = "booker@example.com"
        };

        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        response.EnsureSuccessStatusCode();
        var booking = await response.DeserializeResponse<CreatePublicBookingResponse>();
        booking!.StartTime.Should().Be(DateTimeOffset.Parse(FreeSlotTimeUtc));
        booking.Status.Should().Be(BookingStatus.Accepted);

        // Verify the booking was assigned to the rotating host (member), not the owner
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var savedBooking = await db.Set<Booking>().IgnoreQueryFilters().FirstAsync(b => b.Id == new BookingId(booking.Id));
        savedBooking.OwnerUserId.Should().Be(DatabaseSeeder.Tenant1Member.Id!);
    }

    // ─── Booking fails when all rotating hosts are busy ──────────────────────

    [Fact]
    public async Task CreatePublicBooking_WhenAllRotatingHostsBusy_ShouldReturnConflict()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeAsync();

        await SeedHostBookingAsync(DatabaseSeeder.Tenant1Member.Id!, eventType.Id, DateTimeOffset.Parse(SlotTimeUtc), 30);

        var command = new
        {
            handle,
            eventSlug = eventType.Slug,
            startTime = SlotTimeUtc,
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Test Booker",
            bookerEmail = "booker@example.com"
        };

        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Conflict, "The selected slot is no longer available.");
    }

    // ─── Reassign booking ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReassignBooking_WhenAdminReassigns_ShouldUpdateOwnerAndEmitTelemetry()
    {
        var (handle, eventType) = await SetupRoundRobinEventTypeAsync();

        var bookingResponse = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", new
        {
            handle,
            eventSlug = eventType.Slug,
            startTime = FreeSlotTimeUtc,
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Test Booker",
            bookerEmail = "booker@example.com"
        });
        bookingResponse.EnsureSuccessStatusCode();
        var created = (await bookingResponse.DeserializeResponse<CreatePublicBookingResponse>())!;

        // Reassign to the owner
        var reassignResponse = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/bookings/{created.Id}/reassign",
            new ReassignRoundRobinBookingRequest(DatabaseSeeder.Tenant1Owner.Id!)
        );

        reassignResponse.EnsureSuccessStatusCode();

        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var savedBooking = await db.Set<Booking>().IgnoreQueryFilters().FirstAsync(b => b.Id == new BookingId(created.Id));
        savedBooking!.OwnerUserId.Should().Be(DatabaseSeeder.Tenant1Owner.Id!);
    }

    [Fact]
    public async Task ReassignBooking_WhenBookingNotRoundRobin_ShouldReturnBadRequest()
    {
        // Arrange: create a team event type with Default scheduling (no hosts added)
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var teamClient = CreateTeamEventTypeClient();
        var eventType = await CreateTeamEventTypeViaClientAsync(teamClient, schedule.Id);

        // Create a booking for the Default event type via public API
        var bookingResponse = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", new
        {
            handle = "owner",
            eventSlug = eventType.Slug,
            startTime = FreeSlotTimeUtc,
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Test Booker",
            bookerEmail = "booker@example.com"
        });
        bookingResponse.EnsureSuccessStatusCode();
        var created = (await bookingResponse.DeserializeResponse<CreatePublicBookingResponse>())!;

        // Act: try to reassign via the round-robin endpoint
        var response = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/bookings/{created.Id}/reassign",
            new ReassignRoundRobinBookingRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Only round-robin bookings can be reassigned.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(string Handle, EventTypeInfo EventType)> SetupRoundRobinEventTypeAsync(bool addSecondRotatingHost = false)
    {
        var handle = "owner";
        await UpdateSchedulingProfileAsync(handle);
        var schedule = await CreateScheduleAsync();
        var teamClient = CreateTeamEventTypeClient();
        var eventType = await CreateTeamEventTypeViaClientAsync(teamClient, schedule.Id);

        // Add member as a rotating (non-fixed) host
        var addHostResponse = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!, IsFixed: false, Priority: 0, Weight: 100)
        );
        addHostResponse.EnsureSuccessStatusCode();

        if (addSecondRotatingHost)
        {
            // Owner also becomes a rotating host for tests that need two rotating hosts
            var ownerAsHost = await _rrClient.PostAsJsonAsync(
                $"/api/round-robin/{eventType.Id}/hosts",
                new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Owner.Id!, IsFixed: false, Priority: 0, Weight: 100)
            );
            ownerAsHost.EnsureSuccessStatusCode();
        }

        return (handle, eventType);
    }

    private async Task<(string Handle, EventTypeInfo EventType)> SetupRoundRobinEventTypeWithFixedHostAsync()
    {
        var handle = "owner";
        await UpdateSchedulingProfileAsync(handle);
        var schedule = await CreateScheduleAsync();
        var teamClient = CreateTeamEventTypeClient();
        var eventType = await CreateTeamEventTypeViaClientAsync(teamClient, schedule.Id);

        // Owner as fixed host (always attends)
        await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Owner.Id!, IsFixed: true, Priority: 0, Weight: 100)
        );

        // Member as rotating host
        await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!, IsFixed: false, Priority: 0, Weight: 100)
        );

        return (handle, eventType);
    }

    private async Task SeedHostBookingAsync(UserId hostUserId, string eventTypeId, DateTimeOffset startTime, int durationMinutes)
    {
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();

        var booking = Booking.Create(
            DatabaseSeeder.TenantId,
            hostUserId,
            new EventTypeId(eventTypeId),
            startTime,
            durationMinutes,
            beforeEventBufferMinutes: 0,
            afterEventBufferMinutes: 0,
            "Host Event",
            "host@example.com",
            "UTC", BookingStatus.Accepted,
            new Dictionary<string, string>()
        );
        dbContext.Set<Booking>().Add(booking);
        await dbContext.SaveChangesAsync();
    }

    private async Task UpdateSchedulingProfileAsync(string handle)
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = (string?)null }
        );
        response.EnsureSuccessStatusCode();
    }

    private async Task<ScheduleIdResponse> CreateScheduleAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = true,
            availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } }
        });
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleIdResponse>())!;
    }

    private HttpClient CreateTeamEventTypeClient()
    {
        var ownerWithTeam = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            ActiveTeamId = DatabaseSeeder.TenantId
        };
        return CreateAuthenticatedHttpClient(ownerWithTeam);
    }

    private async Task<EventTypeInfo> CreateTeamEventTypeViaClientAsync(HttpClient client, string scheduleId)
    {
        var slug = $"rr-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/event-types", new
        {
            title = "RR event",
            slug,
            durationMinutes = 30,
            hidden = false,
            scheduleId,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 0
        });
        response.EnsureSuccessStatusCode();
        var result = (await response.DeserializeResponse<EventTypeIdResponse>())!;
        return new EventTypeInfo(result.Id, slug);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleIdResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeIdResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeInfo(string Id, string Slug);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSlotsResponse(Dictionary<string, PublicSlotResponse[]> Slots);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSlotResponse(DateTimeOffset Time, DateTimeOffset EndTime);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CreatePublicBookingResponse(string Id, DateTimeOffset StartTime, DateTimeOffset EndTime, BookingStatus Status);
}
