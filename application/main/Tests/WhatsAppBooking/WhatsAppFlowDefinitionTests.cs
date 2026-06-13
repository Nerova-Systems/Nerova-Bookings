using System.Text.Json;
using Main.Features.WhatsAppBooking.Infrastructure;
using Xunit;

namespace Main.Tests.WhatsAppBooking;

public sealed class WhatsAppFlowDefinitionTests
{
    private static readonly JsonDocumentOptions StrictJson = new(); // Default: no trailing commas, no comments

    [Fact]
    public void LoginFlowDefinition_WhenBuilt_ShouldBeStrictlyValidJson()
    {
        // Act
        using var document = JsonDocument.Parse(WhatsAppLoginFlowDefinition.Build(), StrictJson);

        // Assert
        Assert.Equal("7.3", document.RootElement.GetProperty("version").GetString());
        Assert.True(document.RootElement.GetProperty("screens").GetArrayLength() > 0);
    }

    [Fact]
    public void BookingFlowDefinition_WhenBuilt_ShouldBeStrictlyValidJson()
    {
        // Act
        using var document = JsonDocument.Parse(WhatsAppBookingFlowDefinition.Build(), StrictJson);

        // Assert
        Assert.Equal("7.3", document.RootElement.GetProperty("version").GetString());
        Assert.True(document.RootElement.GetProperty("screens").GetArrayLength() > 0);
    }
}
