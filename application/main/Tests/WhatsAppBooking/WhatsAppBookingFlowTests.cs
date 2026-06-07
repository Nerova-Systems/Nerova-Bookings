using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.WhatsAppBooking;

/// <summary>
///     End-to-end tests for the deterministic WhatsApp booking pipeline: a signed Meta webhook drives the
///     conversation engine, which auto-replies and turns a submitted Flow into a booking + client. Uses the real
///     mediator, repositories, and in-memory database; only the Meta Graph send is faked (MockMetaGraphClient,
///     selected automatically when no Meta credentials are configured in tests).
/// </summary>
public sealed class WhatsAppBookingFlowTests : EndpointBaseTest<MainDbContext>
{
    private const string WebhookUrl = "/api/main/whatsapp/webhook";
    private const string WabaId = "555000111222333";

    // MockMetaGraphClient returns "{wabaId}-phone" as the phone number id during onboarding.
    private static string PhoneNumberId => $"{WabaId}-phone";

    // Meta:AppSecret is empty in test config, so the signature is an HMAC over an empty key.
    private static string ComputeValidSignature(string payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(string.Empty), Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public async Task PostWebhook_WhenInboundText_ShouldStartConversationAndAutoReply()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        const string customer = "+27820001111";
        var payload = BuildTextPayload(customer, "wamid.booking.text.1", "Hi");

        // Act
        var response = await PostSignedWebhookAsync(payload);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_conversations WHERE customer_phone_number = '{customer}'", []).Should().Be(1);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound'", []).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PostWebhook_WhenFlowCompletion_ShouldCreateBookingAndClientAndConfirm()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var scheduleId = await CreateScheduleAsync();
        await CreateEventTypeAsync(scheduleId, "Product demo", "product-demo");
        await OnboardWhatsAppAsync();
        var slot = await GetFirstAvailableSlotAsync("product-demo");
        const string customer = "+27820002222";
        var responseJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["event_slug"] = "product-demo",
            ["start_time"] = slot.ToString("o"),
            ["duration"] = 30,
            ["timezone"] = "Africa/Johannesburg",
            ["booker_name"] = "Thandi Mokoena",
            ["booker_email"] = "thandi@example.com"
        });
        var payload = BuildFlowCompletionPayload(customer, "wamid.booking.flow.1", responseJson);

        // Act
        var response = await PostSignedWebhookAsync(payload);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM bookings WHERE booker_email = 'thandi@example.com'", []).Should().Be(1);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM bookings WHERE booker_phone = '{customer}'", []).Should().Be(1);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM clients WHERE phone_number = '{customer}'", []).Should().Be(1);
        Connection.ExecuteScalar<string>($"SELECT state FROM whats_app_conversations WHERE customer_phone_number = '{customer}'", []).Should().Be("Confirmed");
    }

    [Fact]
    public async Task PostWebhook_WhenFlowCompletionIsUnparseable_ShouldNotCreateBooking()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var scheduleId = await CreateScheduleAsync();
        await CreateEventTypeAsync(scheduleId, "Product demo", "product-demo");
        await OnboardWhatsAppAsync();
        const string customer = "+27820003333";
        var payload = BuildFlowCompletionPayload(customer, "wamid.booking.flow.2", "not-json");

        // Act
        var response = await PostSignedWebhookAsync(payload);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM bookings WHERE booker_phone = '{customer}'", []).Should().Be(0);
    }

    [Fact]
    public async Task GetConversations_WhenConversationsExist_ShouldReturnRecordForTenant()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        const string customer = "+27820004444";
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.conv.list.1", "Hi"))).EnsureSuccessStatusCode();

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/whatsapp/conversations");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.DeserializeResponse<GetWhatsAppConversationsTestResponse>();
        body!.Conversations.Should().ContainSingle(conversation => conversation.CustomerPhoneNumber == customer);
        body.Conversations.Single(conversation => conversation.CustomerPhoneNumber == customer).OutboundCount.Should().BeGreaterThanOrEqualTo(1);
    }

    private async Task OnboardWhatsAppAsync()
    {
        var onboardCommand = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", onboardCommand)).EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> PostSignedWebhookAsync(string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", ComputeValidSignature(payload));
        return await AnonymousHttpClient.SendAsync(request);
    }

    private async Task<DateTimeOffset> GetFirstAvailableSlotAsync(string eventSlug)
    {
        var rangeStart = TimeProvider.System.GetUtcNow().UtcDateTime.Date.AddDays(2);
        var rangeEnd = rangeStart.AddDays(10);
        var url = $"/api/public/slots?handle=owner&eventSlug={eventSlug}&startTime={rangeStart:yyyy-MM-dd}T00:00:00Z&endTime={rangeEnd:yyyy-MM-dd}T00:00:00Z&timeZone=Africa/Johannesburg&duration=30";

        var response = await AnonymousHttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();

        var firstSlot = slots!.Slots.Values
            .SelectMany(daySlots => daySlots)
            .Select(slot => slot.Time)
            .OrderBy(time => time)
            .FirstOrDefault();
        firstSlot.Should().NotBe(default, "the seeded weekday schedule should expose at least one future slot");
        return firstSlot;
    }

    private async Task UpdateSchedulingProfileAsync(string handle)
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = (string?)null }
        );
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> CreateScheduleAsync()
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
        return (await response.DeserializeResponse<ScheduleResponse>())!.Id;
    }

    private async Task CreateEventTypeAsync(string scheduleId, string title, string slug)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types",
            new
            {
                title,
                slug,
                description = "A short product demo",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0,
                locationType = "link",
                locationValue = "https://example.com/meet",
                settings = (object?)null
            }
        );
        response.EnsureSuccessStatusCode();
    }

    private static string BuildTextPayload(string fromNumber, string messageId, string text)
    {
        return $$"""
                 {
                   "object": "whatsapp_business_account",
                   "entry": [{
                     "id": "{{WabaId}}",
                     "changes": [{
                       "value": {
                         "metadata": { "phone_number_id": "{{PhoneNumberId}}", "display_phone_number": "+1 555-0100" },
                         "messages": [{
                           "id": "{{messageId}}",
                           "from": "{{fromNumber}}",
                           "timestamp": "1700000000",
                           "type": "text",
                           "text": { "body": "{{text}}" }
                         }]
                       }
                     }]
                   }]
                 }
                 """;
    }

    private static string BuildFlowCompletionPayload(string fromNumber, string messageId, string responseJson)
    {
        var encodedResponseJson = JsonSerializer.Serialize(responseJson);
        return $$"""
                 {
                   "object": "whatsapp_business_account",
                   "entry": [{
                     "id": "{{WabaId}}",
                     "changes": [{
                       "value": {
                         "metadata": { "phone_number_id": "{{PhoneNumberId}}", "display_phone_number": "+1 555-0100" },
                         "messages": [{
                           "id": "{{messageId}}",
                           "from": "{{fromNumber}}",
                           "timestamp": "1700000000",
                           "type": "interactive",
                           "interactive": {
                             "type": "nfm_reply",
                             "nfm_reply": { "name": "flow", "body": "Sent", "response_json": {{encodedResponseJson}} }
                           }
                         }]
                       }
                     }]
                   }]
                 }
                 """;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSlotsResponse(Dictionary<string, PublicSlotResponse[]> Slots);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSlotResponse(DateTimeOffset Time, DateTimeOffset EndTime);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record GetWhatsAppConversationsTestResponse(WhatsAppConversationTestItem[] Conversations);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WhatsAppConversationTestItem(string CustomerPhoneNumber, string State, string? BookingId, int InboundCount, int OutboundCount);
}
