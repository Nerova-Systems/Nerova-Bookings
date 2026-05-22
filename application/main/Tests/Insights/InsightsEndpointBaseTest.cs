using System.Net.Http.Json;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.Insights.Shared;
using SharedKernel.Authentication;
using SharedKernel.Tests;

namespace Main.Tests.Insights;

/// <summary>
///     Shared base for all insights endpoint tests.
///     Provides a feature-flagged HTTP client and booking setup helpers.
/// </summary>
public abstract class InsightsEndpointBaseTest : EndpointBaseTest<MainDbContext>
{
    protected readonly HttpClient InsightsClient;

    protected InsightsEndpointBaseTest()
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
            FeatureFlags = new HashSet<string> { InsightsAuthorization.InsightsFeatureFlagKey }
        };
        InsightsClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    protected async Task UpdateSchedulingProfileAsync(string handle)
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = "https://example.com/avatar.png" }
        );
        response.EnsureSuccessStatusCode();
    }

    protected async Task<ScheduleTestResponse> CreateScheduleAsync()
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
        return (await response.DeserializeResponse<ScheduleTestResponse>())!;
    }

    protected async Task<EventTypeTestResponse> CreateEventTypeAsync(string scheduleId, string title, string slug)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types",
            new
            {
                title, slug,
                description = "Test",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0,
                locationType = "link",
                locationValue = "https://meet.example.com"
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeTestResponse>())!;
    }

    protected async Task<BookingTestResponse> CreateBookingAsync(string eventSlug, string startTime)
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
                bookerName = "Test Booker",
                bookerEmail = "booker@example.com",
                responses = new Dictionary<string, string> { ["topic"] = "Test" }
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<BookingTestResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    protected sealed record ScheduleTestResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    protected sealed record EventTypeTestResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    protected sealed record BookingTestResponse(string Id);
}
