using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Payments.Paystack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Main.Tests.Payments;

public sealed class PaystackPaymentLinkServiceTests
{
    [Fact]
    public async Task CreatePaymentLinkAsync_OnSuccess_SendsSubaccountAndAmount_AndParsesAuthorizationUrl()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new RecordingHandler(async request =>
            {
                captured = request;
                body = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                                                {
                                                  "status": true,
                                                  "data": {
                                                    "authorization_url": "https://checkout.paystack.com/abc",
                                                    "access_code": "ACCESS_123",
                                                    "reference": "nerova_booking_42"
                                                  }
                                                }
                                                """)
                };
            }
        );
        var client = BuildClient(handler, secretKey: "sk_test_xyz");

        var result = await client.CreatePaymentLinkAsync(
            subaccountCode: "ACCT_subxyz",
            amountMinorUnits: 50_000,
            currency: "NGN",
            customerEmail: "buyer@example.com",
            reference: "nerova_booking_42",
            callbackUrl: "https://app.nerova.test/callback",
            metadata: new Dictionary<string, string> { ["booking_id"] = "B-42" },
            cancellationToken: CancellationToken.None
        );

        result.Should().NotBeNull();
        result!.AuthorizationUrl.Should().Be("https://checkout.paystack.com/abc");
        result.AccessCode.Should().Be("ACCESS_123");
        result.Reference.Should().Be("nerova_booking_42");

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://paystack.test/transaction/initialize");
        captured.Headers.Authorization!.Parameter.Should().Be("sk_test_xyz");

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("subaccount").GetString().Should().Be("ACCT_subxyz");
        doc.RootElement.GetProperty("amount").GetInt64().Should().Be(50_000);
        doc.RootElement.GetProperty("currency").GetString().Should().Be("NGN");
        doc.RootElement.GetProperty("email").GetString().Should().Be("buyer@example.com");
        doc.RootElement.GetProperty("callback_url").GetString().Should().Be("https://app.nerova.test/callback");
        doc.RootElement.GetProperty("metadata").GetProperty("booking_id").GetString().Should().Be("B-42");
    }

    [Fact]
    public async Task CreatePaymentLinkAsync_WhenSecretKeyMissing_ReturnsNullWithoutCallingApi()
    {
        var called = false;
        var handler = new RecordingHandler(_ =>
            {
                called = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        );
        var client = BuildClient(handler, secretKey: null);

        var result = await client.CreatePaymentLinkAsync(
            "ACCT", 100, "NGN", "x@y.z", "ref", null, null, CancellationToken.None
        );

        result.Should().BeNull();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePaymentLinkAsync_OnHttpError_ReturnsNull()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"status\":false,\"message\":\"bad\"}")
            }
        ));
        var client = BuildClient(handler, secretKey: "sk_test_xyz");

        var result = await client.CreatePaymentLinkAsync(
            "ACCT", 100, "NGN", "x@y.z", "ref", null, null, CancellationToken.None
        );

        result.Should().BeNull();
    }

    private static PaystackPaymentLinkService BuildClient(HttpMessageHandler handler, string? secretKey)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(PaystackPaymentLinkService.HttpClientName).Returns(_ => new HttpClient(handler));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Paystack:SecretKey"] = secretKey,
                    ["Paystack:BaseUrl"] = "https://paystack.test/"
                }
            )
            .Build();
        return new PaystackPaymentLinkService(factory, configuration, NullLogger<PaystackPaymentLinkService>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request);
    }
}
