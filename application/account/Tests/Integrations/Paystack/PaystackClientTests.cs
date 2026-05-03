using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Account.Tests.Integrations.Paystack;

public sealed class PaystackClientTests
{
    [Fact]
    public async Task UpdateCustomerBillingInfoAsync_ShouldSendMetadataAsObject()
    {
        // Arrange
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = true, data = new { customer_code = "CUS_test_123" } })
            }
        );
        var client = CreateClient(handler);
        var billingInfo = new BillingInfo(
            "Test Organization",
            new BillingAddress("Vestergade 12", null, "1456", "Copenhagen", null, "DK"),
            "billing@example.com",
            "DK12345678"
        );

        // Act
        var result = await client.UpdateCustomerBillingInfoAsync(PaystackCustomerId.NewId("CUS_test_123"), billingInfo, "en-US", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        handler.RequestBody.Should().NotBeNull();
        using var document = JsonDocument.Parse(handler.RequestBody!);
        document.RootElement.GetProperty("metadata").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ShouldSendMetadataAsObject()
    {
        // Arrange
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                    {
                        status = true,
                        data = new { authorization_url = "https://checkout.paystack.com/test", reference = "NB-sub-test" }
                    }
                )
            }
        );
        var client = CreateClient(handler);

        // Act
        var result = await client.CreateCheckoutSessionAsync(PaystackCustomerId.NewId("CUS_test_123"), "billing@example.com", SubscriptionPlan.Standard, "en-US", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        handler.RequestBody.Should().NotBeNull();
        using var document = JsonDocument.Parse(handler.RequestBody!);
        document.RootElement.GetProperty("metadata").ValueKind.Should().Be(JsonValueKind.Object);
    }

    private static PaystackClient CreateClient(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Paystack:BaseUrl"] = "https://api.paystack.test",
                    ["Paystack:SecretKey"] = "sk_test_123",
                    ["Paystack:Plans:Standard:Code"] = "PLN_standard"
                }
            )
            .Build();

        var httpClientFactory = new TestHttpClientFactory(new HttpClient(handler));
        return new PaystackClient(httpClientFactory, configuration, NullLogger<PaystackClient>.Instance);
    }

    private sealed class TestHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
