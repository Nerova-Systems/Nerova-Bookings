using System.Net;
using System.Net.Http.Json;
using Main.Database;
using Main.Features;
using Main.Features.WhatsAppMessaging.Commands;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Validation;
using Xunit;

namespace Main.Tests.WhatsAppMessaging;

public sealed class SendWhatsAppMessageTests : EndpointBaseTest<MainDbContext>
{
    private const string MessagesUrl = "/api/main/whatsapp/messages";
    private const string WabaId = "123456789012345";

    private async Task OnboardAsync()
    {
        var command = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = $"{WabaId}-phone" };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SendMessage_WhenValidAndOnboarded_ShouldCreateOutboundMessageAndCollectTelemetry()
    {
        // Arrange
        await OnboardAsync();
        var command = new { to = "+15550001234", text = "Hello from the test!" };
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(MessagesUrl, command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SendWhatsAppMessageResponse>();
        result!.MessageId.Should().StartWith("wamsg_");

        var messageCount = Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM whats_app_messages WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        messageCount.Should().Be(1);

        var direction = Connection.ExecuteScalar<string>(
            "SELECT direction FROM whats_app_messages WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        direction.Should().Be("Outbound");

        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e is WhatsAppMessageSent);
    }

    [Fact]
    public async Task SendMessage_WhenNoWabaConnected_ShouldReturnBadRequest()
    {
        // Arrange — no WABA onboarded
        var command = new { to = "+15550001234", text = "Hello" };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(MessagesUrl, command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No WhatsApp Business Account is connected for this tenant.");
    }

    [Fact]
    public async Task SendMessage_WhenToIsEmpty_ShouldReturnValidationError()
    {
        // Arrange
        var command = new { to = "", text = "Hello" };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(MessagesUrl, command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, [
                new ErrorDetail("To", "The recipient phone number is required.")
            ]
        );
    }

    [Fact]
    public async Task SendMessage_WhenTextIsEmpty_ShouldReturnValidationError()
    {
        // Arrange
        var command = new { to = "+15550001234", text = "" };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(MessagesUrl, command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, [
                new ErrorDetail("Text", "The message text is required.")
            ]
        );
    }

    [Fact]
    public async Task SendMessage_WhenTextExceedsMaxLength_ShouldReturnValidationError()
    {
        // Arrange
        var command = new { to = "+15550001234", text = new string('x', 4097) };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(MessagesUrl, command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, [
                new ErrorDetail("Text", "The message text must not exceed 4096 characters.")
            ]
        );
    }

    [Fact]
    public async Task SendMessage_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new { to = "+15550001234", text = "Hello" };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(MessagesUrl, command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
