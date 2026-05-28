using Account.Features.WhatsApp.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit tests for <see cref="WhatsAppInternalApiKeyValidator" /> — the thin abstraction that
///     replaced the inline <c>IsAuthorized</c> check in <c>WhatsAppInternalEndpoints</c>.
/// </summary>
public sealed class WhatsAppInternalApiKeyValidatorTests
{
    private static WhatsAppInternalApiKeyValidator BuildValidator(string? configuredKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["WhatsApp:InternalApiKey"] = configuredKey })
            .Build();
        return new WhatsAppInternalApiKeyValidator(config);
    }

    [Fact]
    public void IsValid_WithCorrectKey_ReturnsTrue()
    {
        var validator = BuildValidator("super-secret");

        validator.IsValid("ApiKey super-secret").Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithWrongKey_ReturnsFalse()
    {
        var validator = BuildValidator("super-secret");

        validator.IsValid("ApiKey wrong-key").Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithMissingHeader_ReturnsFalse()
    {
        var validator = BuildValidator("super-secret");

        validator.IsValid(null).Should().BeFalse();
    }
}
