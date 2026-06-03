using System.Net.Http.Json;
using FluentAssertions;
using Main.Database;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Queries;
using Main.Integrations.Meta;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.WhatsAppOnboarding;

public sealed class GetWhatsAppOnboardingStatusTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task GetStatus_WhenNotOnboarded_ShouldReturnNotConnected()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/whatsapp/status");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetWhatsAppOnboardingStatusResponse>();
        result!.IsConnected.Should().BeFalse();
        result.BusinessName.Should().BeNull();
        result.PhoneNumber.Should().BeNull();
        result.Status.Should().Be(nameof(WhatsAppBusinessAccountStatus.NotConnected));
    }

    [Fact]
    public async Task GetStatus_WhenOnboarded_ShouldReturnConnectedDetails()
    {
        // Arrange
        var command = new { code = "valid-auth-code", wabaId = "123456789012345", phoneNumberId = "987654321098765" };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command)).EnsureSuccessStatusCode();

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/whatsapp/status");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetWhatsAppOnboardingStatusResponse>();
        result!.IsConnected.Should().BeTrue();
        result.BusinessName.Should().Be(MockMetaGraphClient.MockBusinessName);
        result.PhoneNumber.Should().Be(MockMetaGraphClient.MockDisplayPhoneNumber);
        result.Status.Should().Be(nameof(WhatsAppBusinessAccountStatus.Connected));
    }
}
