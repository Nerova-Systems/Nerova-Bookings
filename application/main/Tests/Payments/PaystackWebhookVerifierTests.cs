using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Main.Features.Payments.Paystack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Main.Tests.Payments;

public sealed class PaystackWebhookVerifierTests
{
    private const string SecretKey = "sk_test_webhook_secret";

    [Fact]
    public void Verify_WithValidSignatureAndPayload_ReturnsEventDetails()
    {
        const string payload = """
                               {
                                 "event": "charge.success",
                                 "data": {
                                   "id": "evt_abc",
                                   "reference": "nerova_booking_42"
                                 }
                               }
                               """;
        var verifier = CreateVerifier(SecretKey);
        var signature = Sign(SecretKey, payload);

        var result = verifier.Verify(payload, signature);

        result.Should().NotBeNull();
        result!.EventId.Should().Be("evt_abc");
        result.EventType.Should().Be("charge.success");
        result.Reference.Should().Be("nerova_booking_42");
    }

    [Fact]
    public void Verify_WithInvalidSignature_ReturnsNull()
    {
        const string payload = """{"event":"charge.success","data":{"id":"evt_abc","reference":"r"}}""";
        var verifier = CreateVerifier(SecretKey);

        var result = verifier.Verify(payload, "deadbeef");

        result.Should().BeNull();
    }

    [Fact]
    public void Verify_WithMissingSecretKey_ReturnsNull()
    {
        const string payload = """{"event":"charge.success","data":{"id":"e","reference":"r"}}""";
        var verifier = CreateVerifier(null);
        var signature = Sign("any", payload);

        var result = verifier.Verify(payload, signature);

        result.Should().BeNull();
    }

    [Fact]
    public void Verify_WithMissingEventId_FallsBackToReference()
    {
        const string payload = """{"event":"charge.failed","data":{"reference":"nerova_booking_99"}}""";
        var verifier = CreateVerifier(SecretKey);
        var signature = Sign(SecretKey, payload);

        var result = verifier.Verify(payload, signature);

        result.Should().NotBeNull();
        result!.EventType.Should().Be("charge.failed");
        result.EventId.Should().Be("nerova_booking_99");
        result.Reference.Should().Be("nerova_booking_99");
    }

    [Fact]
    public void Verify_WithMalformedJson_ReturnsNull()
    {
        const string payload = "this is not json";
        var verifier = CreateVerifier(SecretKey);
        var signature = Sign(SecretKey, payload);

        var result = verifier.Verify(payload, signature);

        result.Should().BeNull();
    }

    private static PaystackWebhookVerifier CreateVerifier(string? secretKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Paystack:SecretKey"] = secretKey
                }
            )
            .Build();
        return new PaystackWebhookVerifier(configuration, NullLogger<PaystackWebhookVerifier>.Instance);
    }

    private static string Sign(string secretKey, string payload)
        => Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(payload))
        ).ToLowerInvariant();
}
