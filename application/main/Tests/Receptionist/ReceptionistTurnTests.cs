using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Main.Database;
using Main.Features.Clients.Domain;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Receptionist;

/// <summary>
///     End-to-end tests for the AI receptionist pipeline (docs/agentic-system-spec.md §6.7): a signed
///     Meta webhook drives routing, identity gating, tool execution, session persistence, budgets, and
///     escalation. The model is the deterministic ScriptedChatClient (selected automatically because no
///     AI API key is configured in tests); its behavior is scripted through @tool/@reply directives in
///     the inbound customer message.
/// </summary>
public sealed class ReceptionistTurnTests : EndpointBaseTest<MainDbContext>
{
    private const string WebhookUrl = "/api/main/whatsapp/webhook";
    private const string WabaId = "555000111222333";

    private static string PhoneNumberId => $"{WabaId}-phone";

    [Fact]
    public async Task PostWebhook_WhenReceptionistDisabled_ShouldUseFlowsEngineAndCreateNoSession()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        const string customer = "+27830001111";
        var payload = BuildTextPayload(customer, "wamid.rec.off.1", "Hi");

        // Act
        var response = await PostSignedWebhookAsync(payload);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_conversations WHERE customer_phone_number = '{customer}'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM receptionist_sessions", []).Should().Be(0);
    }

    [Fact]
    public async Task PostWebhook_WhenReceptionistEnabled_ShouldReplyAndPersistSession()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830002222";
        var payload = BuildTextPayload(customer, "wamid.rec.on.1", "@reply Hello from your front desk!");

        // Act
        var response = await PostSignedWebhookAsync(payload);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound' AND text = 'Hello from your front desk!'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM receptionist_sessions", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT turn_count FROM receptionist_sessions", []).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT agent_thread FROM receptionist_sessions", []).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostWebhook_WhenSecondMessageArrives_ShouldResumeSameSession()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830003333";
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.resume.1", "@reply First reply"))).EnsureSuccessStatusCode();

        // Act
        var response = await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.resume.2", "@reply Second reply"));

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM receptionist_sessions", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT turn_count FROM receptionist_sessions", []).Should().Be(2);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound' AND text = 'Second reply'", []).Should().Be(1);
    }

    [Fact]
    public async Task PostWebhook_WhenUnidentifiedCustomerTriesToBook_ShouldNeverCreateBooking()
    {
        // Arrange
        await SetUpBookableServiceAsync();
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830004444";
        var script = """@tool CreateBooking {\"serviceSlug\":\"product-demo\",\"startTime\":\"2030-06-13T09:00:00Z\"}\n@reply done""";

        // Act
        var response = await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.gate.1", script));

        // Assert — write tools are absent for unidentified conversations (spec R3), so no booking ever happens.
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM bookings WHERE booker_phone = '{customer}'", []).Should().Be(0);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound'", []).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PostWebhook_WhenIdentifiedCustomerBooks_ShouldCreateBookingThroughAgent()
    {
        // Arrange
        await SetUpBookableServiceAsync();
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830005555";
        InsertClient(customer, "Thandi", "Mokoena", "thandi.rec@example.com");
        var slot = await GetFirstAvailableSlotAsync("product-demo");
        var script = $$"""@tool CreateBooking {\"serviceSlug\":\"product-demo\",\"startTime\":\"{{slot:O}}\"}\n@reply You are booked!""";

        // Act
        var response = await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.book.1", script));

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM bookings WHERE booker_phone = '{customer}'", []).Should().Be(1);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound' AND text = 'You are booked!'", []).Should().Be(1);
    }

    [Fact]
    public async Task PostWebhook_WhenAgentEscalates_ShouldCreateEscalationAndGoSilent()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830006666";
        var script = """@tool EscalateToHuman {\"reason\":\"Customer complaint\",\"summary\":\"Customer is unhappy with their last visit.\"}\n@reply A team member will contact you shortly.""";

        // Act
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.esc.1", script))).EnsureSuccessStatusCode();

        // Assert
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM escalations WHERE status = 'Open'", []).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT state FROM receptionist_sessions", []).Should().Be("Escalated");

        var outboundBefore = Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound'", []);
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.esc.2", "@reply should not appear"))).EnsureSuccessStatusCode();
        var outboundAfterFirstFollowUp = Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound'", []);
        outboundAfterFirstFollowUp.Should().Be(outboundBefore + 1, "exactly one courteous hold message is sent (spec R6)");

        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.esc.3", "@reply still should not appear"))).EnsureSuccessStatusCode();
        var outboundAfterSecondFollowUp = Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound'", []);
        outboundAfterSecondFollowUp.Should().Be(outboundAfterFirstFollowUp, "an escalated conversation stays silent after the hold message");
    }

    [Fact]
    public async Task ResolveEscalation_WhenOwnerResolves_ShouldReactivateAgent()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830007777";
        var script = """@tool EscalateToHuman {\"reason\":\"Needs human\",\"summary\":\"Question requiring judgment.\"}\n@reply Hold on.""";
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.res.1", script))).EnsureSuccessStatusCode();
        var escalations = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>("/api/main/receptionist/escalations");
        var escalationId = escalations.GetProperty("escalations")[0].GetProperty("id").GetString();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var resolveResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/main/receptionist/escalations/{escalationId}/resolve", new { dismiss = false, resolutionNote = "Called the customer." });

        // Assert
        resolveResponse.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string>("SELECT status FROM escalations", []).Should().Be("Resolved");
        Connection.ExecuteScalar<string>("SELECT state FROM receptionist_sessions", []).Should().Be("Active");
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(telemetryEvent => telemetryEvent.GetType().Name == "EscalationResolved");

        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.res.2", "@reply Welcome back!"))).EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE to_phone_number = '{customer}' AND direction = 'Outbound' AND text = 'Welcome back!'", []).Should().Be(1);
    }

    [Fact]
    public async Task PostWebhook_WhenSessionTokenBudgetExceeded_ShouldEscalateInsteadOfRunning()
    {
        // Arrange
        await OnboardWhatsAppAsync();
        await EnableReceptionistAsync();
        const string customer = "+27830008888";
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.budget.1", "@reply ok"))).EnsureSuccessStatusCode();
        var sessionId = Connection.ExecuteScalar<string>("SELECT id FROM receptionist_sessions", []);
        Connection.Update("receptionist_sessions", "id", sessionId, [("input_tokens", 999_999_999L)]);

        // Act
        (await PostSignedWebhookAsync(BuildTextPayload(customer, "wamid.rec.budget.2", "@reply should not run"))).EnsureSuccessStatusCode();

        // Assert
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM escalations WHERE reason = 'Session token budget exceeded'", []).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT state FROM receptionist_sessions", []).Should().Be("Escalated");
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM whats_app_messages WHERE direction = 'Outbound' AND to_phone_number = '{customer}' AND text = 'should not run'", []).Should().Be(0);
    }

    [Fact]
    public async Task GetReceptionistSettings_WhenNoneSaved_ShouldReturnDefaults()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>("/api/main/receptionist/settings");

        // Assert
        response.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        response.GetProperty("languages")[0].GetString().Should().Be("English");
    }

    [Fact]
    public async Task UpdateReceptionistSettings_WhenValid_ShouldPersistAndRoundTrip()
    {
        // Act
        var putResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/main/receptionist/settings",
            new { isEnabled = true, tone = "Playful", languages = new[] { "English", "isiZulu" }, faqNotes = "We are closed on Mondays." }
        );

        // Assert
        putResponse.EnsureSuccessStatusCode();
        var settings = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>("/api/main/receptionist/settings");
        settings.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        settings.GetProperty("tone").GetString().Should().Be("Playful");
        settings.GetProperty("languages").GetArrayLength().Should().Be(2);
        settings.GetProperty("faqNotes").GetString().Should().Be("We are closed on Mondays.");
    }

    [Fact]
    public async Task GetEscalations_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/main/receptionist/escalations");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private async Task EnableReceptionistAsync()
    {
        // The receptionist needs the tenant's public scheduling profile (business name + booking handle).
        (await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/scheduling/profile", new { handle = "owner", displayName = "Owner Name", avatarUrl = (string?)null })).EnsureSuccessStatusCode();

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/main/receptionist/settings",
            new { isEnabled = true, tone = "Friendly", languages = new[] { "English" }, faqNotes = (string?)null }
        );
        response.EnsureSuccessStatusCode();
    }

    private async Task OnboardWhatsAppAsync()
    {
        var onboardCommand = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", onboardCommand)).EnsureSuccessStatusCode();
    }

    private async Task SetUpBookableServiceAsync()
    {
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
                title = "Product demo",
                slug = "product-demo",
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
        eventTypeResponse.EnsureSuccessStatusCode();
    }

    private void InsertClient(string phoneNumber, string firstName, string lastName, string email)
    {
        Connection.Insert("clients", [
                ("tenant_id", DatabaseSeeder.TenantId.Value),
                ("id", ClientId.NewId().ToString()),
                ("created_at", TimeProvider.GetUtcNow().AddDays(-30)),
                ("modified_at", null),
                ("first_name", firstName),
                ("last_name", lastName),
                ("email", email),
                ("phone_number", phoneNumber),
                ("avatar_url", null),
                ("last_visit_at", null)
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
        firstSlot.Should().NotBe(default, "the weekday schedule should expose at least one future slot");
        return firstSlot;
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

    private static string ComputeValidSignature(string payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(string.Empty), Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
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
}
