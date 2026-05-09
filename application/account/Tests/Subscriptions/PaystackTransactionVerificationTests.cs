using System.Net;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class PaystackTransactionVerificationTests
{
    [Fact]
    public async Task VerifyTransaction_WhenApiStatusSucceedsButTransactionStatusFails_ShouldReturnUnpaid()
    {
        // Arrange
        var client = CreateClient(CreateVerificationPayload("failed", "card", reusable: true));

        // Act
        var result = await client.VerifyTransactionAsync("ref_123", PaystackPaymentPurpose.Subscribe, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Paid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTransaction_WhenChannelIsNotCard_ShouldRejectPayment()
    {
        // Arrange
        var client = CreateClient(CreateVerificationPayload("success", "bank", reusable: true));

        // Act
        var result = await client.VerifyTransactionAsync("ref_123", PaystackPaymentPurpose.Subscribe, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Paid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Only card payments are accepted.");
    }

    [Fact]
    public async Task VerifyTransaction_WhenAuthorizationIsNotReusable_ShouldRejectPayment()
    {
        // Arrange
        var client = CreateClient(CreateVerificationPayload("success", "card", reusable: false));

        // Act
        var result = await client.VerifyTransactionAsync("ref_123", PaystackPaymentPurpose.Subscribe, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Paid.Should().BeFalse();
        result.ErrorMessage.Should().Be("The card authorization is not reusable.");
    }

    private static PaystackClient CreateClient(string responseBody)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Paystack:SecretKey"] = "sk_test_transaction_secret"
                }
            )
            .Build();

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new StubHttpMessageHandler(responseBody)));

        return new PaystackClient(configuration, factory, Substitute.For<ILogger<PaystackClient>>());
    }

    private static string CreateVerificationPayload(string transactionStatus, string channel, bool reusable)
    {
        return $$"""
                 {
                   "status": true,
                   "data": {
                     "status": "{{transactionStatus}}",
                     "reference": "ref_123",
                     "amount": 2900,
                     "currency": "USD",
                     "channel": "{{channel}}",
                     "metadata": {
                       "purpose": "Subscribe"
                     },
                     "customer": {
                       "email": "billing@example.com",
                       "customer_code": "CUS_123"
                     },
                     "authorization": {
                       "authorization_code": "AUTH_123",
                       "email": "billing@example.com",
                       "signature": "SIG_123",
                       "brand": "visa",
                       "last4": "4242",
                       "exp_month": "12",
                       "exp_year": "2028",
                       "reusable": {{reusable.ToString().ToLowerInvariant()}}
                     }
                   }
                 }
                 """;
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody)
                }
            );
        }
    }
}
