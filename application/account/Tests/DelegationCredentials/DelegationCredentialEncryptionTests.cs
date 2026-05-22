using Account.Features.DelegationCredentials.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Account.Tests.DelegationCredentials;

/// <summary>
///     Tests for <see cref="DelegationCredentialEncryption" /> using an ephemeral
///     <see cref="EphemeralDataProtectionProvider" /> so no key ring is required.
/// </summary>
public sealed class DelegationCredentialEncryptionTests
{
    private readonly DelegationCredentialEncryption _encryption =
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_ShouldReturnDifferentValueThanPlaintext()
    {
        const string plaintext = """{"type":"service_account","project_id":"my-proj"}""";

        var ciphertext = _encryption.Protect(plaintext);

        ciphertext.Should().NotBe(plaintext);
        ciphertext.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Protect_ThenUnprotect_ShouldRoundTripCorrectly()
    {
        const string plaintext = """{"type":"service_account","project_id":"my-proj"}""";

        var ciphertext = _encryption.Protect(plaintext);
        var recovered = _encryption.Unprotect(ciphertext);

        recovered.Should().Be(plaintext);
    }

    [Fact]
    public void Protect_CalledTwice_ShouldReturnDifferentCiphertexts()
    {
        const string plaintext = "same_input";

        var ct1 = _encryption.Protect(plaintext);
        var ct2 = _encryption.Protect(plaintext);

        // Data Protection is nonce-based; two protect calls must differ.
        ct1.Should().NotBe(ct2);
    }

    [Fact]
    public void Protect_EmptyString_ShouldStillEncryptAndDecrypt()
    {
        const string plaintext = "";

        var ciphertext = _encryption.Protect(plaintext);
        var recovered = _encryption.Unprotect(ciphertext);

        recovered.Should().Be(plaintext);
    }

    [Fact]
    public void Unprotect_WithTamperedCiphertext_ShouldThrow()
    {
        var act = () => _encryption.Unprotect("this_is_not_a_valid_ciphertext");

        act.Should().Throw<Exception>();
    }
}
