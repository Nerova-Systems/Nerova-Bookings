using System.Security.Cryptography;
using System.Text;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class PaystackWebhookSignatureTests
{
    [Fact]
    public void VerifyWebhookSignature_WhenPayloadHasValidPaystackHmac_ShouldReturnWebhookEvent()
    {
        // Arrange
        const string secretKey = "sk_test_webhook_secret";
        const string payload = """
                               {
                                 "event": "charge.success",
                                 "data": {
                                   "id": "evt_123",
                                   "reference": "nerova_subscribe_123",
                                   "customer": {
                                     "customer_code": "CUS_123"
                                   }
                                 }
                               }
                               """;
        var client = CreateClient(secretKey);
        var signature = CreateSignature(secretKey, payload);

        // Act
        var result = client.VerifyWebhookSignature(payload, signature);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be("evt_123");
        result.EventType.Should().Be("charge.success");
        result.Reference.Should().Be("nerova_subscribe_123");
        result.CustomerId.Should().Be(PaystackCustomerId.NewId("CUS_123"));
    }

    [Fact]
    public void VerifyWebhookSignature_WhenPayloadHasInvalidPaystackHmac_ShouldReturnNull()
    {
        // Arrange
        const string payload = """{"event":"charge.success","data":{"id":"evt_123"}}""";
        var client = CreateClient("sk_test_webhook_secret");

        // Act
        var result = client.VerifyWebhookSignature(payload, "invalid_signature");

        // Assert
        result.Should().BeNull();
    }

    private static PaystackClient CreateClient(string secretKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Paystack:SecretKey"] = secretKey
                }
            )
            .Build();

        return new PaystackClient(
            configuration,
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<ILogger<PaystackClient>>()
        );
    }

    private static string CreateSignature(string secretKey, string payload)
    {
        return Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
