using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.BookingSideEffects.Workers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Integrations.Email;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class BookingSideEffectsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task CreateWorkflow_WhenValid_ShouldPersistEmailWorkflow()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/workflows",
            new
            {
                name = "Booking created email",
                active = true,
                trigger = "BOOKING_CREATED",
                scheduledOffsetMinutes = (int?)null,
                steps = new[] { new { kind = "email", recipient = "booker", subject = "Your booking is confirmed", body = "Thanks for booking." } }
            }
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var workflow = await response.DeserializeResponse<WorkflowResponse>();
        workflow!.Name.Should().Be("Booking created email");
        workflow.Trigger.Should().Be("BOOKING_CREATED");
        workflow.Steps.Should().ContainSingle().Which.Subject.Should().Be("Your booking is confirmed");
        Connection.RowExists("workflows", workflow.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CreateWebhook_WhenValid_ShouldPersistSubscription()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/webhooks",
            new
            {
                active = true,
                subscriberUrl = "https://example.com/cal/webhook",
                secret = "top-secret",
                triggers = new[] { "BOOKING_CREATED", "BOOKING_CONFIRMED" },
                payloadFormat = "cal-com",
                payloadVersion = "v1"
            }
        );

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var webhook = await response.DeserializeResponse<WebhookSubscriptionResponse>();
        webhook!.SubscriberUrl.Should().Be("https://example.com/cal/webhook");
        webhook.Triggers.Should().Equal("BOOKING_CREATED", "BOOKING_CONFIRMED");
        Connection.RowExists("webhook_subscriptions", webhook.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CreatePublicBooking_WhenWorkflowAndWebhookAreActive_ShouldEnqueueSideEffectsOnce()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWorkflowAsync(eventType.Id, "BOOKING_CREATED");
        await CreateWebhookAsync(eventType.Id, "BOOKING_CREATED");

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug = "intro-call",
                startTime = "2026-06-01T07:00:00Z",
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName = "Ada Lovelace",
                bookerEmail = "ada@example.com",
                responses = new Dictionary<string, string>()
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        var booking = await response.DeserializeResponse<CreatePublicBookingResponse>();
        Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM booking_side_effect_deliveries WHERE booking_id = @booking_id AND trigger = @trigger",
            [new { booking_id = booking!.Id, trigger = "BOOKING_CREATED" }]
        ).Should().Be(2);
        Connection.ExecuteScalar<long>(
            "SELECT COUNT(DISTINCT dedupe_key) FROM booking_side_effect_deliveries WHERE booking_id = @booking_id",
            [new { booking_id = booking.Id }]
        ).Should().Be(2);
    }

    [Fact]
    public async Task ConfirmBooking_WhenWebhookIsActive_ShouldEnqueueConfirmedWebhook()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", new { confirmationPolicy = new { requiresConfirmation = true } });
        await CreateWebhookAsync(eventType.Id, "BOOKING_CONFIRMED");
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/bookings/{booking.Id}/confirm", null);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM booking_side_effect_deliveries WHERE booking_id = @booking_id AND trigger = @trigger AND kind = @kind",
            [new { booking_id = booking.Id, trigger = "BOOKING_CONFIRMED", kind = "webhook" }]
        ).Should().Be(1);
    }

    [Fact]
    public async Task ProcessPendingDeliveries_WhenEmailDeliveryIsPending_ShouldSendEmailAndMarkSent()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWorkflowAsync(eventType.Id, "BOOKING_CREATED");
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        using var scope = Provider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<BookingSideEffectProcessor>();

        // Act
        var processed = await processor.ProcessPendingAsync(10, CancellationToken.None);

        // Assert
        processed.Should().Be(1);
        await EmailClient.Received(1).SendAsync(
            Arg.Is<EmailMessage>(message =>
                message.Recipient == "ada@example.com" &&
                message.Subject == "Booking created for Intro call" &&
                message.PlainTextBody.Contains("Ada Lovelace", StringComparison.Ordinal)
            ),
            Arg.Any<CancellationToken>()
        );
        Connection.ExecuteScalar<string>(
            "SELECT status FROM booking_side_effect_deliveries WHERE booking_id = @booking_id AND kind = @kind",
            [new { booking_id = booking.Id, kind = "email" }]
        ).Should().Be("sent");
    }

    [Fact]
    public async Task CreateWebhook_WhenUrlIsNotHttp_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/webhooks",
            new
            {
                active = true,
                subscriberUrl = "ftp://example.com/cal/webhook",
                secret = "top-secret",
                triggers = new[] { "BOOKING_CREATED" },
                payloadFormat = "cal-com",
                payloadVersion = "v1"
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
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
                responses = new Dictionary<string, string>()
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<CreatePublicBookingResponse>())!;
    }

    private async Task<WorkflowResponse> CreateWorkflowAsync(string eventTypeId, string trigger)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventTypeId}/workflows",
            new
            {
                name = $"{trigger} email",
                active = true,
                trigger,
                scheduledOffsetMinutes = (int?)null,
                steps = new[] { new { kind = "email", recipient = "booker", subject = (string?)null, body = (string?)null } }
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<WorkflowResponse>())!;
    }

    private async Task<WebhookSubscriptionResponse> CreateWebhookAsync(string eventTypeId, string trigger)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventTypeId}/webhooks",
            new
            {
                active = true,
                subscriberUrl = "https://example.com/cal/webhook",
                secret = "top-secret",
                triggers = new[] { trigger },
                payloadFormat = "cal-com",
                payloadVersion = "v1"
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<WebhookSubscriptionResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowResponse(string Id, string Name, bool Active, string Trigger, int? ScheduledOffsetMinutes, WorkflowStepResponse[] Steps);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowStepResponse(string Kind, string Recipient, string? Subject, string? Body);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WebhookSubscriptionResponse(string Id, bool Active, string SubscriberUrl, string? Secret, string[] Triggers, string PayloadFormat, string PayloadVersion);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CreatePublicBookingResponse(string Id);
}
