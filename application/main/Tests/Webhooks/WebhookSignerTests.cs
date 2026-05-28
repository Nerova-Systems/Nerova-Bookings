using FluentAssertions;
using Main.Features.Webhooks.Infrastructure;
using Xunit;

namespace Main.Tests.Webhooks;

public sealed class WebhookSignerTests
{
    [Fact]
    public void Sign_WithRfcTestVector_ShouldReturnExpectedHex()
    {
        // RFC HMAC-SHA256 test vector — also matches cal.com's sendPayload output shape.
        const string secret = "key";
        const string body = "The quick brown fox jumps over the lazy dog";

        var signature = WebhookSigner.Sign(secret, body);

        signature.Should().Be("sha256=f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8");
    }

    [Fact]
    public void Sign_WithDifferentSecret_ShouldProduceDifferentSignature()
    {
        const string body = "{\"event\":\"BOOKING_CREATED\"}";
        var first = WebhookSigner.Sign("secret-one", body);
        var second = WebhookSigner.Sign("secret-two", body);

        first.Should().NotBe(second);
    }

    [Fact]
    public void HeaderName_ShouldMatchCalComConvention()
    {
        WebhookSigner.HeaderName.Should().Be("X-Cal-Signature-256");
    }
}
