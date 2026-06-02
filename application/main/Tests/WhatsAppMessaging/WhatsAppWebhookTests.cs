using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Main.Database;
using Main.Features.WhatsAppMessaging.Domain;
using Main.Integrations.Meta;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.WhatsAppMessaging;

public sealed class WhatsAppWebhookTests : EndpointBaseTest<MainDbContext>
{
    private const string WebhookUrl = "/api/main/whatsapp/webhook";
    private const string WabaId = "123456789012345";
    private const string PhoneNumberId = "123456789012345-phone"; // MockMetaGraphClient returns "{wabaId}-phone"

    // Meta:AppSecret is not set in test config, so defaults to empty string
    private static string ComputeValidSignature(string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(string.Empty);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildInboundMessagePayload(string phoneNumberId, string fromNumber = "+15550001234", string messageId = "wamid.test001")
    {
        return $$"""
                 {
                   "object": "whatsapp_business_account",
                   "entry": [{
                     "id": "{{WabaId}}",
                     "changes": [{
                       "value": {
                         "metadata": {
                           "phone_number_id": "{{phoneNumberId}}",
                           "display_phone_number": "+1 555-0100"
                         },
                         "messages": [{
                           "id": "{{messageId}}",
                           "from": "{{fromNumber}}",
                           "timestamp": "1700000000",
                           "text": { "body": "Hello World" }
                         }]
                       }
                     }]
                   }]
                 }
                 """;
    }

    private static string BuildStatusPayload(string metaMessageId, string status)
    {
        return $$"""
                 {
                   "object": "whatsapp_business_account",
                   "entry": [{
                     "id": "{{WabaId}}",
                     "changes": [{
                       "value": {
                         "metadata": {
                           "phone_number_id": "{{PhoneNumberId}}",
                           "display_phone_number": "+1 555-0100"
                         },
                         "statuses": [{
                           "id": "{{metaMessageId}}",
                           "status": "{{status}}",
                           "timestamp": "1700000001"
                         }]
                       }
                     }]
                   }]
                 }
                 """;
    }

    [Fact]
    public async Task VerifyWebhook_WhenCorrectToken_ShouldReturnChallenge()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync($"{WebhookUrl}?hub.mode=subscribe&hub.verify_token=&hub.challenge=my_challenge_value");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("my_challenge_value");
    }

    [Fact]
    public async Task VerifyWebhook_WhenWrongToken_ShouldReturnForbidden()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync($"{WebhookUrl}?hub.mode=subscribe&hub.verify_token=wrong_token&hub.challenge=my_challenge");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWebhook_WhenValidSignature_ShouldReturnSuccessAndInsertPendingEvent()
    {
        // Arrange
        var payload = """{"object":"whatsapp_business_account","entry":[]}""";
        var signature = ComputeValidSignature(payload);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", signature);
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var eventCount = Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_events", []);
        eventCount.Should().Be(1);

        var eventStatus = Connection.ExecuteScalar<string>("SELECT status FROM whats_app_events", []);
        // Event is processed synchronously in tests (phase 2 runs inline), so may be Processed
        eventStatus.Should().BeOneOf(nameof(WhatsAppEventStatus.Pending), nameof(WhatsAppEventStatus.Processed));
    }

    [Fact]
    public async Task PostWebhook_WhenInvalidSignature_ShouldReturnUnauthorized()
    {
        // Arrange
        var payload = """{"object":"whatsapp_business_account","entry":[]}""";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", "sha256=invalidhash");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Unauthorized, "Invalid webhook signature.");
    }

    [Fact]
    public async Task PostWebhook_WhenMissingSignatureHeader_ShouldReturnUnauthorized()
    {
        // Arrange
        var payload = """{"object":"whatsapp_business_account","entry":[]}""";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Unauthorized, "X-Hub-Signature-256 header missing or duplicated.");
    }

    [Fact]
    public async Task PostWebhook_WhenDuplicatePayload_ShouldReturnSuccessAndInsertOnlyOneEvent()
    {
        // Arrange
        var payload = """{"object":"whatsapp_business_account","entry":[],"deduptest":true}""";
        var signature = ComputeValidSignature(payload);

        var request1 = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("X-Hub-Signature-256", signature);
        (await AnonymousHttpClient.SendAsync(request1)).EnsureSuccessStatusCode();

        // Act – send identical payload again
        var request2 = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("X-Hub-Signature-256", signature);
        var response = await AnonymousHttpClient.SendAsync(request2);

        // Assert
        response.EnsureSuccessStatusCode();

        var eventCount = Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_events", []);
        eventCount.Should().Be(1);
    }

    [Fact]
    public async Task PostWebhook_WhenInboundMessage_ShouldCreateWhatsAppMessageWithDirectionInbound()
    {
        // Arrange — onboard WABA so phone number ID is known
        var onboardCommand = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", onboardCommand)).EnsureSuccessStatusCode();

        var payload = BuildInboundMessagePayload(PhoneNumberId, messageId: "wamid.inbound001");
        var signature = ComputeValidSignature(payload);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", signature);
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var messageCount = Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_messages WHERE meta_message_id = 'wamid.inbound001'", []);
        messageCount.Should().Be(1);

        var direction = Connection.ExecuteScalar<string>("SELECT direction FROM whats_app_messages WHERE meta_message_id = 'wamid.inbound001'", []);
        direction.Should().Be(nameof(MessageDirection.Inbound));
    }

    [Fact]
    public async Task PostWebhook_WhenStatusUpdate_ShouldUpdateMessageStatus()
    {
        // Arrange — onboard WABA and send a message to have an existing outbound record
        var onboardCommand = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", onboardCommand)).EnsureSuccessStatusCode();

        // Send outbound message to get a metaMessageId stored
        var sendCommand = new { to = "+15550001234", text = "Test outbound" };
        var sendResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/messages", sendCommand);
        sendResponse.EnsureSuccessStatusCode();

        // Find the meta_message_id stored (starts with "wamid.MOCK_")
        var metaMessageId = Connection.ExecuteScalar<string>("SELECT meta_message_id FROM whats_app_messages", []);
        metaMessageId.Should().StartWith("wamid.MOCK_");

        // Act — deliver a status update webhook for this message
        var payload = BuildStatusPayload(metaMessageId!, "delivered");
        var signature = ComputeValidSignature(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", signature);
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var status = Connection.ExecuteScalar<string>($"SELECT status FROM whats_app_messages WHERE meta_message_id = '{metaMessageId}'", []);
        status.Should().Be(nameof(WhatsAppMessageStatus.Delivered));
    }
}
