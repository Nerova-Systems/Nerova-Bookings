using Account.Features.WhatsApp.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Pure unit tests for <see cref="WabaEncryptionService" />.
///     No database or DI required — each test instantiates the service directly.
/// </summary>
public sealed class WabaEncryptionServiceTests
{
    private readonly WabaEncryptionService _service = new();

    [Fact]
    public void GenerateKeyPair_ShouldReturnWellFormedPems()
    {
        var result = _service.GenerateKeyPair("test-passphrase");

        result.PublicKeyPem.Should().StartWith("-----BEGIN PUBLIC KEY-----");
        result.EncryptedPrivateKeyBase64.Should().NotBeNullOrEmpty();
        result.IvBase64.Should().NotBeNullOrEmpty();
        result.Fingerprint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateKeyPair_ThenDecryptPrivateKey_ShouldRoundTrip()
    {
        const string passphrase = "test-passphrase-round-trip";

        var result = _service.GenerateKeyPair(passphrase);
        var decryptedPem = _service.DecryptPrivateKey(result.EncryptedPrivateKeyBase64, result.IvBase64, passphrase);

        decryptedPem.Should().StartWith("-----BEGIN PRIVATE KEY-----");
    }

    [Fact]
    public void ComputePublicKeyFingerprint_ShouldReturn64LowercaseHexChars()
    {
        var result = _service.GenerateKeyPair("any-passphrase");

        result.Fingerprint.Should().HaveLength(64);
        result.Fingerprint.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void GenerateKeyPair_TwoCalls_ShouldProduceDifferentKeyPairs()
    {
        var first = _service.GenerateKeyPair("same-passphrase");
        var second = _service.GenerateKeyPair("same-passphrase");

        first.PublicKeyPem.Should().NotBe(second.PublicKeyPem);
        first.Fingerprint.Should().NotBe(second.Fingerprint);
    }
}
