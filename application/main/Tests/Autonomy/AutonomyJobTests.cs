using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Main.Database;
using Main.Features.Autonomy.Commands;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Autonomy;

/// <summary>
///     End-to-end tests for the autonomy jobs framework (docs/maf-autonomy-design.md): detection is
///     idempotent per trigger, L1 parks suggestions for the owner's tap, approval executes through the
///     real command path and counts the promotion streak, and the ladder endpoints control levels.
/// </summary>
public sealed class AutonomyJobTests : EndpointBaseTest<MainDbContext>
{
    private const string RoutesPrefix = "/api/main/autonomy";
    private const string WabaId = "555000111222333";

    [Fact]
    public async Task RunAutonomyTick_WhenDepositPendingTooLong_ShouldCreateSuggestionAtLevelOne()
    {
        // Arrange
        await SetUpTenantWithPendingDepositAsync("+27830010001");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        await RunTickAsync();

        // Assert
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM job_runs WHERE job_type = 'payment-recovery' AND status = 'AwaitingApproval'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_messages WHERE direction = 'Outbound' AND text LIKE '%deposit%'", []).Should().Be(0);
    }

    [Fact]
    public async Task RunAutonomyTick_WhenRunTwice_ShouldNotDuplicateRuns()
    {
        // Arrange
        await SetUpTenantWithPendingDepositAsync("+27830010002");

        // Act
        await RunTickAsync();
        await RunTickAsync();

        // Assert
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM job_runs WHERE job_type = 'payment-recovery'", []).Should().Be(1);
    }

    [Fact]
    public async Task ApproveJobRun_ShouldExecuteSendReminderAndCountStreak()
    {
        // Arrange
        await SetUpTenantWithPendingDepositAsync("+27830010003");
        await RunTickAsync();
        var jobRuns = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>($"{RoutesPrefix}/job-runs?status=AwaitingApproval");
        var runId = jobRuns.GetProperty("jobRuns")[0].GetProperty("id").GetString();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"{RoutesPrefix}/job-runs/{runId}/approve", new { });

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("receipt").GetString().Should().Contain("Reminded");
        Connection.ExecuteScalar<string>($"SELECT status FROM job_runs WHERE id = '{runId}'", []).Should().Be("Completed");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_messages WHERE direction = 'Outbound' AND text LIKE '%deposit%'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT approvals_streak FROM tenant_job_policies WHERE job_type = 'payment-recovery'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM bookings WHERE payment_reminder_sent_at IS NOT NULL", []).Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(telemetryEvent => telemetryEvent.GetType().Name == "JobSuggestionResolved");
    }

    [Fact]
    public async Task DismissJobRun_ShouldSkipWithoutExecuting()
    {
        // Arrange
        await SetUpTenantWithPendingDepositAsync("+27830010004");
        await RunTickAsync();
        var jobRuns = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>($"{RoutesPrefix}/job-runs?status=AwaitingApproval");
        var runId = jobRuns.GetProperty("jobRuns")[0].GetProperty("id").GetString();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"{RoutesPrefix}/job-runs/{runId}/dismiss", new { });

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string>($"SELECT status FROM job_runs WHERE id = '{runId}'", []).Should().Be("Skipped");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_messages WHERE direction = 'Outbound' AND text LIKE '%deposit%'", []).Should().Be(0);
    }

    [Fact]
    public async Task SetJobPolicyLevel_ToActAndTell_ShouldExecuteOnNextTick()
    {
        // Arrange
        await SetUpTenantWithPendingDepositAsync("+27830010005");
        (await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"{RoutesPrefix}/policies", new { jobType = "payment-recovery", level = 2 })).EnsureSuccessStatusCode();

        // Act
        await RunTickAsync();

        // Assert
        Connection.ExecuteScalar<string>("SELECT status FROM job_runs WHERE job_type = 'payment-recovery'", []).Should().Be("Completed");
        Connection.ExecuteScalar<string>("SELECT receipt FROM job_runs WHERE job_type = 'payment-recovery'", []).Should().Contain("Reminded");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_messages WHERE direction = 'Outbound' AND text LIKE '%deposit%'", []).Should().Be(1);
    }

    [Fact]
    public async Task GetJobPolicies_ShouldListAllRegisteredJobsWithDefaults()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>($"{RoutesPrefix}/policies");

        // Assert
        var policies = response.GetProperty("policies").EnumerateArray().ToArray();
        policies.Select(policy => policy.GetProperty("jobType").GetString()).Should().Contain(["payment-recovery", "rebook-cancelled", "weekly-digest"]);
        policies.First(policy => policy.GetProperty("jobType").GetString() == "payment-recovery").GetProperty("level").GetInt32().Should().Be(1);
        policies.First(policy => policy.GetProperty("jobType").GetString() == "weekly-digest").GetProperty("level").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetJobRuns_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync($"{RoutesPrefix}/job-runs");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private async Task RunTickAsync()
    {
        // Dispatch the runner exactly as the TickerQ cron job does, scoped to the seeded tenant.
        using var scope = Provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new RunAutonomyTickCommand(DatabaseSeeder.TenantId, BypassQuietHours: true));
        result.IsSuccess.Should().BeTrue();
    }

    private async Task SetUpTenantWithPendingDepositAsync(string customerPhone)
    {
        (await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/scheduling/profile", new { handle = "owner", displayName = "Owner Name", avatarUrl = (string?)null })).EnsureSuccessStatusCode();
        var onboardCommand = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = $"{WabaId}-phone" };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", onboardCommand)).EnsureSuccessStatusCode();

        var scheduleResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/schedules",
            new
            {
                name = "Working hours",
                timeZone = "Africa/Johannesburg",
                isDefault = true,
                availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } },
                dateOverrides = Array.Empty<object>()
            }
        );
        scheduleResponse.EnsureSuccessStatusCode();
        var scheduleId = (await scheduleResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var eventTypeResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types",
            new
            {
                title = "Gel set",
                slug = "gel-set",
                description = "Gel manicure",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0,
                locationType = "in-person",
                locationValue = "Salon",
                settings = (object?)null
            }
        );
        eventTypeResponse.EnsureSuccessStatusCode();

        var slot = await GetFirstAvailableSlotAsync("gel-set");
        var bookingResponse = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug = "gel-set",
                startTime = slot.ToString("o"),
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName = "Thandi Mokoena",
                bookerEmail = "thandi.autonomy@example.com",
                bookerPhone = customerPhone
            }
        );
        bookingResponse.EnsureSuccessStatusCode();
        var bookingId = (await bookingResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Deposit pending for three hours with the payment link issued and no reminder sent yet.
        var threeHoursAgo = TimeProvider.GetUtcNow().AddHours(-3);
        Connection.Update("bookings", "id", bookingId, [
                ("payment_status", "Pending"),
                ("payment_reference", $"dep_{bookingId}"),
                ("payment_link_url", "https://checkout.paystack.test/abc"),
                ("payment_state_changed_at", threeHoursAgo),
                ("payment_reminder_sent_at", null)
            ]
        );
    }

    private async Task<DateTimeOffset> GetFirstAvailableSlotAsync(string eventSlug)
    {
        var rangeStart = TimeProvider.GetUtcNow().UtcDateTime.Date.AddDays(2);
        var rangeEnd = rangeStart.AddDays(10);
        var url = $"/api/public/slots?handle=owner&eventSlug={eventSlug}&startTime={rangeStart:yyyy-MM-dd}T00:00:00Z&endTime={rangeEnd:yyyy-MM-dd}T00:00:00Z&timeZone=Africa/Johannesburg&duration=30";

        var response = await AnonymousHttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var slots = await response.Content.ReadFromJsonAsync<JsonElement>();

        var firstSlot = slots.GetProperty("slots").EnumerateObject()
            .SelectMany(day => day.Value.EnumerateArray())
            .Select(slot => slot.GetProperty("time").GetDateTimeOffset())
            .OrderBy(time => time)
            .FirstOrDefault();
        firstSlot.Should().NotBe(default);
        return firstSlot;
    }
}
