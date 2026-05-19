using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task GetEventTypeDeliveries_WhenSideEffectsExist_ShouldReturnDeliverySummaries()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWorkflowAsync(eventType.Id, "BOOKING_CREATED");
        await CreateWebhookAsync(eventType.Id, "BOOKING_CREATED");
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}/side-effect-deliveries");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var deliveries = await response.DeserializeResponse<BookingSideEffectDeliveriesResponse>();
        deliveries!.Deliveries.Should().HaveCount(2);
        deliveries.Deliveries.Should().OnlyContain(delivery => delivery.BookingId == booking.Id && delivery.Trigger == "BOOKING_CREATED" && delivery.Status == "pending");
        deliveries.Deliveries.Select(delivery => delivery.Kind).Should().BeEquivalentTo("email", "webhook");
    }

    [Fact]
    public async Task GetBookingDeliveries_WhenSideEffectsExist_ShouldReturnOnlyThatBooking()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWorkflowAsync(eventType.Id, "BOOKING_CREATED");
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");
        var expectedBooking = await CreateBookingAsync("intro-call", "2026-06-02T07:00:00Z", "Grace Hopper", "grace@example.com");

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/bookings/{expectedBooking.Id}/side-effects");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var deliveries = await response.DeserializeResponse<BookingSideEffectDeliveriesResponse>();
        deliveries!.Deliveries.Should().ContainSingle();
        deliveries.Deliveries[0].BookingId.Should().Be(expectedBooking.Id);
    }

    [Fact]
    public async Task CreatePublicBooking_WhenWorkflowAndWebhookAreInactive_ShouldNotEnqueueDeliveries()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWorkflowAsync(eventType.Id, "BOOKING_CREATED", false);
        await CreateWebhookAsync(eventType.Id, "BOOKING_CREATED", false);

        // Act
        var booking = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Assert
        Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM booking_side_effect_deliveries WHERE booking_id = @booking_id",
            [new { booking_id = booking.Id }]
        ).Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingDeliveries_WhenRunTwice_ShouldNotDuplicateSentEmail()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWorkflowAsync(eventType.Id, "BOOKING_CREATED");
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        using var scope = Provider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<BookingSideEffectProcessor>();

        // Act
        var firstRun = await processor.ProcessPendingAsync(10, CancellationToken.None);
        var secondRun = await processor.ProcessPendingAsync(10, CancellationToken.None);

        // Assert
        firstRun.Should().Be(1);
        secondRun.Should().Be(0);
        await EmailClient.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingDeliveries_WhenWebhookFails_ShouldRetryThenMarkFailedAtMaxAttempts()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWebhookAsync(eventType.Id, "BOOKING_CREATED", subscriberUrl: "http://localhost:1/cal/webhook");
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<BookingSideEffectProcessor>();

        // Act
        var delivery = await ProcessDueWebhookAsync(processor, dbContext);
        for (var attempt = delivery.Attempts; attempt < 5; attempt++)
        {
            await MarkWebhookRetryDueAsync(dbContext, delivery.Id.Value);
            delivery = await ProcessDueWebhookAsync(processor, dbContext);
        }

        // Assert
        delivery.Status.Should().Be("failed");
        delivery.Attempts.Should().Be(5);
        delivery.NextRetryAt.Should().BeNull();
        delivery.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessPendingDeliveries_WhenWebhookSucceeds_ShouldSendCalCompatibleHeadersAndSignature()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateWebhookAsync(eventType.Id, "BOOKING_CREATED");
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        using var scope = Provider.CreateScope();
        var httpHandler = new CapturingHttpMessageHandler();
        var processor = new BookingSideEffectProcessor(
            scope.ServiceProvider.GetRequiredService<MainDbContext>(),
            EmailClient,
            new StaticHttpClientFactory(new HttpClient(httpHandler)),
            TimeProvider.System,
            NullLogger<BookingSideEffectProcessor>.Instance
        );

        // Act
        var processed = await processor.ProcessPendingAsync(10, CancellationToken.None);

        // Assert
        processed.Should().Be(1);
        var delivery = await LoadWebhookDeliveryAsync(scope.ServiceProvider.GetRequiredService<MainDbContext>());
        var expectedSignature = ComputeSignature(delivery.PayloadJson, "top-secret");
        httpHandler.Request!.Headers.GetValues("X-Cal-Event").Should().ContainSingle().Which.Should().Be("BOOKING_CREATED");
        httpHandler.Request.Headers.GetValues("X-Cal-Webhook-Version").Should().ContainSingle().Which.Should().Be("v1");
        httpHandler.Request.Headers.GetValues("X-Cal-Signature-256").Should().ContainSingle().Which.Should().Be(expectedSignature);
        delivery.Status.Should().Be("sent");
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

    private static string ComputeSignature(string payloadJson, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        using var hmac = new HMACSHA256(key);
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant()}";
    }

    private static async Task<BookingSideEffectDelivery> ProcessDueWebhookAsync(BookingSideEffectProcessor processor, MainDbContext dbContext)
    {
        await processor.ProcessPendingAsync(10, CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        return await LoadWebhookDeliveryAsync(dbContext);
    }

    private static async Task MarkWebhookRetryDueAsync(MainDbContext dbContext, string deliveryId)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE booking_side_effect_deliveries
             SET next_retry_at = {DateTimeOffset.UtcNow.AddMinutes(-1)}
             WHERE id = {deliveryId}
             """
        );
        dbContext.ChangeTracker.Clear();
    }

    private static async Task<BookingSideEffectDelivery> LoadWebhookDeliveryAsync(MainDbContext dbContext)
    {
        return await dbContext.Set<BookingSideEffectDelivery>()
            .IgnoreQueryFilters()
            .SingleAsync(delivery => delivery.Kind == "webhook");
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

    private async Task<WorkflowResponse> CreateWorkflowAsync(string eventTypeId, string trigger, bool active = true)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventTypeId}/workflows",
            new
            {
                name = $"{trigger} email",
                active,
                trigger,
                scheduledOffsetMinutes = (int?)null,
                steps = new[] { new { kind = "email", recipient = "booker", subject = (string?)null, body = (string?)null } }
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<WorkflowResponse>())!;
    }

    private async Task<WebhookSubscriptionResponse> CreateWebhookAsync(string eventTypeId, string trigger, bool active = true, string subscriberUrl = "https://example.com/cal/webhook")
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventTypeId}/webhooks",
            new
            {
                active,
                subscriberUrl,
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
    private sealed record WorkflowResponse(
        string Id,
        string Name,
        bool Active,
        string Trigger,
        int? ScheduledOffsetMinutes,
        WorkflowStepResponse[] Steps
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowStepResponse(string Kind, string Recipient, string? Subject, string? Body);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WebhookSubscriptionResponse(
        string Id,
        bool Active,
        string SubscriberUrl,
        string? Secret,
        string[] Triggers,
        string PayloadFormat,
        string PayloadVersion
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CreatePublicBookingResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingSideEffectDeliveriesResponse(BookingSideEffectDeliverySummaryResponse[] Deliveries);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record BookingSideEffectDeliverySummaryResponse(
        string Id,
        string BookingId,
        string Trigger,
        string Kind,
        string Status,
        int Attempts,
        DateTimeOffset? NextRetryAt,
        string? LastError
    );

    private sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return httpClient;
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
