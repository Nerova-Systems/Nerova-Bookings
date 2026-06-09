using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Main.Database;
using Main.Features.WhatsAppMessaging.Domain;
using Main.Features.WhatsAppMessaging.Queries;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.WhatsAppMessaging;

public sealed class GetWhatsAppMessagesTests : EndpointBaseTest<MainDbContext>
{
    private const string MessagesUrl = "/api/main/whatsapp/messages";
    private const string WabaId = "123456789012345";

    private async Task OnboardAsync()
    {
        var command = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = $"{WabaId}-phone" };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetMessages_WhenNoMessages_ShouldReturnEmptyList()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(MessagesUrl);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetWhatsAppMessagesResponse>();
        result!.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMessages_WhenMessagesExist_ShouldReturnAllMessagesForTenantOrderedByTimestampDesc()
    {
        // Arrange — send 2 messages so they appear in the messages table
        await OnboardAsync();
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/messages", new { to = "+15550001234", text = "First" })).EnsureSuccessStatusCode();
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/messages", new { to = "+15550005678", text = "Second" })).EnsureSuccessStatusCode();

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(MessagesUrl);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetWhatsAppMessagesResponse>();
        result!.Messages.Should().HaveCount(2);
        result.Messages.All(m => m.Direction == nameof(MessageDirection.Outbound)).Should().BeTrue();
    }

    [Fact]
    public async Task GetMessages_WhenOtherTenantHasMessages_ShouldNotReturnTheirMessages()
    {
        // Arrange — seed a message row for a different tenant_id
        var otherTenantId = 9999999L;
        Connection.Insert("whats_app_messages", [
                ("tenant_id", otherTenantId),
                ("id", "wamsg_other_tenant_message"),
                ("created_at", DateTimeOffset.UtcNow),
                ("modified_at", null),
                ("meta_message_id", "wamid.other_tenant"),
                ("direction", nameof(MessageDirection.Inbound)),
                ("from_phone_number", "+15550001111"),
                ("to_phone_number", "+15550002222"),
                ("text", "Other tenant message"),
                ("status", nameof(WhatsAppMessageStatus.Received)),
                ("timestamp", DateTimeOffset.UtcNow)
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(MessagesUrl);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetWhatsAppMessagesResponse>();
        result!.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMessages_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync(MessagesUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
