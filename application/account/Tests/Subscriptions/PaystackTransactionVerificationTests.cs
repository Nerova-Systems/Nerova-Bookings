using System.Net;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class PaystackTransactionVerificationTests
{
    [Fact]
    public async Task VerifyTransaction_WhenApiStatusSucceedsButTransactionStatusFails_ShouldReturnUnpaid()
    {
        // Arrange
        var client = CreateClient(CreateVerificationPayload("failed", "card", true));

        // Act
        var result = await client.VerifyTransactionAsync("ref_123", PaystackPaymentPurpose.Subscribe, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Paid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTransaction_WhenChannelIsNotCard_ShouldRejectPayment()
    {
        // Arrange
        var client = CreateClient(CreateVerificationPayload("success", "bank", true));

        // Act
        var result = await client.VerifyTransactionAsync("ref_123", PaystackPaymentPurpose.Subscribe, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Paid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Only card payments are accepted.");
    }

    [Fact]
    public async Task VerifyTransaction_WhenAuthorizationIsNotReusable_ShouldRejectPayment()
    {
        // Arrange
        var client = CreateClient(CreateVerificationPayload("success", "card", false));

        // Act
        var result = await client.VerifyTransactionAsync("ref_123", PaystackPaymentPurpose.Subscribe, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Paid.Should().BeFalse();
        result.ErrorMessage.Should().Be("The card authorization is not reusable.");
    }

    [Fact]
    public async Task SyncPaymentTransactions_WhenPaystackReturnsCustomerTransactions_ShouldReturnSuccessfulPaymentTransactions()
    {
        // Arrange
        var client = CreateClient("""
                                  {
                                    "status": true,
                                    "data": [
                                      {
                                        "status": "success",
                                        "reference": "ref_success",
                                        "amount": 2900,
                                        "currency": "ZAR",
                                        "paid_at": "2026-01-02T03:04:05Z",
                                        "customer": { "customer_code": "CUS_123" }
                                      },
                                      {
                                        "status": "failed",
                                        "reference": "ref_failed",
                                        "amount": 9900,
                                        "currency": "ZAR",
                                        "paid_at": null,
                                        "customer": { "customer_code": "CUS_123" }
                                      }
                                    ]
                                  }
                                  """
        );

        // Act
        var transactions = await client.SyncPaymentTransactionsAsync(PaystackCustomerId.NewId("CUS_123"), CancellationToken.None);

        // Assert
        transactions.Should().ContainSingle();
        transactions![0].Amount.Should().Be(29.00m);
        transactions[0].AmountExcludingTax.Should().Be(29.00m);
        transactions[0].Currency.Should().Be("ZAR");
        transactions[0].Status.Should().Be(PaymentTransactionStatus.Succeeded);
        transactions[0].Date.Should().Be(DateTimeOffset.Parse("2026-01-02T03:04:05Z"));
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
