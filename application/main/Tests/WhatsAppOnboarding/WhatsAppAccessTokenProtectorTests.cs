using Main.Features.WhatsAppOnboarding.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Main.Tests.WhatsAppOnboarding;

public sealed class WhatsAppAccessTokenProtectorTests
{
    private static WhatsAppAccessTokenProtector CreateProtector() =>
        new(new EphemeralDataProtectionProvider(), NullLogger<WhatsAppAccessTokenProtector>.Instance);

    [Fact]
    public void ProtectThenUnprotect_ShouldRoundTripAndNotExposePlaintext()
    {
        // Arrange
        var protector = CreateProtector();
        const string accessToken = "super-secret-meta-access-token";

        // Act
        var encrypted = protector.Protect(accessToken);
        var decrypted = protector.Unprotect(encrypted);

        // Assert
        encrypted.Should().NotBe(accessToken);
        decrypted.Should().Be(accessToken);
    }

    [Fact]
    public void Unprotect_WhenCiphertextIsCorrupted_ShouldReturnNull()
    {
        // Arrange
        var protector = CreateProtector();

        // Act
        var result = protector.Unprotect("not-a-valid-ciphertext");

        // Assert
        result.Should().BeNull();
    }
}
