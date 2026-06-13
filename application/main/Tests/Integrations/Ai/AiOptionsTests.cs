using Main.Integrations.Ai;
using Xunit;

namespace Main.Tests.Integrations.Ai;

public sealed class AiOptionsTests
{
    [Fact]
    public void ResolveProvider_WhenNoApiKey_ShouldBeScripted()
    {
        // Arrange
        var options = new AiOptions { Provider = "AzureOpenAI", ApiKey = null };

        // Act & Assert
        Assert.Equal(AiProvider.Scripted, options.ResolveProvider());
    }

    [Theory]
    [InlineData("AzureOpenAI", AiProvider.AzureOpenAi)]
    [InlineData("azureopenai", AiProvider.AzureOpenAi)]
    [InlineData("Anthropic", AiProvider.Anthropic)]
    [InlineData("Scripted", AiProvider.Scripted)]
    [InlineData("", AiProvider.Anthropic)] // Pre-provider behavior: a bare API key means Anthropic
    [InlineData("unknown", AiProvider.Anthropic)]
    public void ResolveProvider_WhenApiKeyConfigured_ShouldHonorProviderSwitch(string provider, AiProvider expected)
    {
        // Arrange
        var options = new AiOptions { Provider = provider, ApiKey = "key" };

        // Act & Assert
        Assert.Equal(expected, options.ResolveProvider());
    }

    [Theory]
    [InlineData("https://api.anthropic.com", "https://api.anthropic.com/v1/messages")]
    [InlineData("https://api.anthropic.com/", "https://api.anthropic.com/v1/messages")]
    [InlineData("https://nerova.services.ai.azure.com/anthropic", "https://nerova.services.ai.azure.com/anthropic/v1/messages")]
    [InlineData("https://nerova.services.ai.azure.com/anthropic/", "https://nerova.services.ai.azure.com/anthropic/v1/messages")]
    public void BuildMessagesUri_ShouldPreserveEndpointBasePath(string endpoint, string expected)
    {
        // Act & Assert: Foundry's /anthropic prefix must survive the join (a naive absolute join strips it)
        Assert.Equal(new Uri(expected), AnthropicChatClient.BuildMessagesUri(endpoint));
    }

    [Fact]
    public void ClampBudgets_WhenValuesAreOutOfRange_ShouldRestoreGuardrails()
    {
        // Arrange — a broken environment tries to disable every budget
        var options = new AiOptions
        {
            MaxToolCallsPerTurn = 0,
            MaxOutputTokensPerTurn = -1,
            MaxTokensPerSession = 0,
            MaxTokensPerTenantPerMonth = long.MaxValue
        };

        // Act
        options.ClampBudgets();

        // Assert
        Assert.Equal(1, options.MaxToolCallsPerTurn);
        Assert.Equal(64, options.MaxOutputTokensPerTurn);
        Assert.Equal(1_000, options.MaxTokensPerSession);
        Assert.Equal(1_000_000_000, options.MaxTokensPerTenantPerMonth);
    }
}
