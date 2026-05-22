using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.Collective.Shared;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Collective;

/// <summary>
///     Integration tests for collective scheduling slot calculation through the public API.
///     Verifies that slots are blocked when ANY host has a conflicting booking.
/// </summary>
public sealed class CollectiveBookingFlowTests : EndpointBaseTest<MainDbContext>
{
    // Fixed test date: Wednesday 2026-06-03 (to avoid weekends)
    // 09:00 SAST = 07:00 UTC; 09:30 SAST = 07:30 UTC
    private static readonly string TestDate = "2026-06-03";
    private static readonly string SlotTimeUtc = $"{TestDate}T07:00:00Z"; // 09:00 SAST
    private static readonly string FreeSlotTimeUtc = $"{TestDate}T08:00:00Z"; // 10:00 SAST
    private readonly HttpClient _collectiveClient;

    public CollectiveBookingFlowTests()
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
            FeatureFlags = new HashSet<string> { CollectiveAuthorization.CollectiveFeatureFlagKey }
        };
        _collectiveClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    // ─── Slot offered when all hosts free ────────────────────────────────────

    [Fact]
    public async Task GetPublicSlots_WhenCollectiveEventTypeWithAllHostsFree_ShouldOfferSlots()
    {
        // Arrange
        var (handle, eventType) = await SetupCollectiveEventTypeAsync();

        // Act
        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle={handle}&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots.Should().ContainKey(TestDate);
        slots.Slots[TestDate].Should().Contain(s => s.Time == DateTimeOffset.Parse(SlotTimeUtc));
    }

    // ─── Slot blocked when one host busy ─────────────────────────────────────

    [Fact]
    public async Task GetPublicSlots_WhenOneHostHasConflictingBooking_ShouldExcludeBlockedSlot()
    {
        // Arrange
        var (handle, eventType) = await SetupCollectiveEventTypeAsync();

        // Seed a conflicting booking for the host (Tenant1Member) at 09:00 SAST
        var hostUserId = DatabaseSeeder.Tenant1Member.Id!;
        await SeedHostBookingAsync(hostUserId, eventType.Id, DateTimeOffset.Parse(SlotTimeUtc), 30);

        // Act
        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle={handle}&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        // The 09:00 SAST slot should be absent because the host is busy
        var allSlots = slots!.Slots.Values.SelectMany(s => s).ToArray();
        allSlots.Should().NotContain(s => s.Time == DateTimeOffset.Parse(SlotTimeUtc));
        // The 10:00 SAST slot should still be offered (host is free)
        allSlots.Should().Contain(s => s.Time == DateTimeOffset.Parse(FreeSlotTimeUtc));
    }

    // ─── Booking succeeds when slot is free ───────────────────────────────────

    [Fact]
    public async Task CreatePublicBooking_WhenCollectiveSlotIsAvailable_ShouldSucceed()
    {
        // Arrange
        var (handle, eventType) = await SetupCollectiveEventTypeAsync();

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

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var booking = await response.DeserializeResponse<CreatePublicBookingResponse>();
        booking!.StartTime.Should().Be(DateTimeOffset.Parse(FreeSlotTimeUtc));
        booking.Status.Should().Be("accepted");
    }

    // ─── Booking fails when host is busy ─────────────────────────────────────

    [Fact]
    public async Task CreatePublicBooking_WhenHostHasConflictingBooking_ShouldReturnConflict()
    {
        // Arrange
        var (handle, eventType) = await SetupCollectiveEventTypeAsync();

        // Seed a conflicting booking for the host at the slot we want to book
        var hostUserId = DatabaseSeeder.Tenant1Member.Id!;
        await SeedHostBookingAsync(hostUserId, eventType.Id, DateTimeOffset.Parse(SlotTimeUtc), 30);

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

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Conflict, "The selected slot is no longer available.");
    }

    // ─── Collective event type with no hosts uses fallback ───────────────────

    [Fact]
    public async Task GetPublicSlots_WhenCollectiveEventTypeHasNoHosts_ShouldOfferSlotsBasedOnOwnerSchedule()
    {
        // Arrange: set up a team event type but DON'T add any hosts
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var teamClient = CreateTeamEventTypeClient();
        var eventType = await CreateTeamEventTypeViaClientAsync(teamClient, schedule.Id);
        // SchedulingType is still Default (no hosts added), so public slot calculator is used

        // Act: public slots should still be offered based on owner schedule
        var response = await AnonymousHttpClient.GetAsync(
            $"/api/public/slots?handle=owner&eventSlug={eventType.Slug}&startTime={TestDate}T00:00:00Z&endTime={TestDate}T23:59:59Z&timeZone=Africa/Johannesburg&duration=30"
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots.Should().ContainKey(TestDate);
        slots.Slots[TestDate].Should().NotBeEmpty();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(string Handle, EventTypeInfo EventType)> SetupCollectiveEventTypeAsync()
    {
        var handle = "owner";
        await UpdateSchedulingProfileAsync(handle);
        var schedule = await CreateScheduleAsync();
        var teamClient = CreateTeamEventTypeClient();
        var eventType = await CreateTeamEventTypeViaClientAsync(teamClient, schedule.Id);

        // Add member as host
        var addHostResponse = await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );
        addHostResponse.EnsureSuccessStatusCode();

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
            0,
            0,
            "Host Event",
            "host@example.com",
            "UTC",
            "accepted",
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
            }
        );
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
        var slug = $"collective-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/event-types", new
            {
                title = "Team event",
                slug,
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0
            }
        );
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
    private sealed record CreatePublicBookingResponse(string Id, DateTimeOffset StartTime, DateTimeOffset EndTime, string Status);
}
