using System.Net;
using System.Text;
using Account.Integrations.PayFast;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Account.Tests.Integrations.PayFast;

public sealed class PayFastRefundTests
{
    [Fact]
    public async Task RefundPaymentAsync_WhenPaymentSourceRefundIsAvailable_ShouldQueryThenCreateRefund()
    {
        var handler = new QueueingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "status": "success",
                      "data": {
                        "status": "REFUNDABLE",
                        "amount_available_for_refund": 5000,
                        "refund_full": { "method": "PAYMENT_SOURCE" },
                        "refund_partial": { "method": "PAYMENT_SOURCE" }
                      }
                    }
                    """
                )
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "code": 200,
                      "status": "success",
                      "data": {
                        "response": true,
                        "message": "Success"
                      }
                    }
                    """
                )
            }
        );
        var client = CreateClient(handler);

        var result = await client.RefundPaymentAsync("pf-123", 40.12m, "Support credit", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Supported.Should().BeTrue();
        result.Reference.Should().Be("pf-123");

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://api.payfast.co.za/refunds/query/pf-123?testing=true");
        handler.Requests[1].Method.Should().Be(HttpMethod.Post);
        handler.Requests[1].RequestUri!.ToString().Should().Be("https://api.payfast.co.za/refunds/pf-123?testing=true");

        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        body.Should().Contain("amount=4012");
        body.Should().Contain("notify_buyer=1");
        body.Should().Contain("notify_merchant=0");
        body.Should().Contain("reason=Support+credit");
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenRefundIsNotAvailable_ShouldReturnUnsupportedWithoutCreatingRefund()
    {
        var handler = new QueueingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "status": "success",
                      "data": {
                        "status": "NOT_AVAILABLE",
                        "errors": ["Refund window has expired"],
                        "amount_available_for_refund": 0,
                        "refund_full": { "method": "NOT_AVAILABLE" },
                        "refund_partial": { "method": "NOT_AVAILABLE" }
                      }
                    }
                    """
                )
            }
        );
        var client = CreateClient(handler);

        var result = await client.RefundPaymentAsync("pf-123", 10m, "Support credit", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Supported.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Refund window has expired");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenRefundRequiresBankPayout_ShouldReturnUnsupportedWithoutCollectingBankDetails()
    {
        var handler = new QueueingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "status": "success",
                      "data": {
                        "status": "REFUNDABLE",
                        "amount_available_for_refund": 5000,
                        "refund_full": { "method": "BANK_PAYOUT" },
                        "refund_partial": { "method": "BANK_PAYOUT" }
                      }
                    }
                    """
                )
            }
        );
        var client = CreateClient(handler);

        var result = await client.RefundPaymentAsync("pf-123", 10m, "Support credit", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Supported.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bank payout");
        handler.Requests.Should().ContainSingle();
    }

    private static PayFastClient CreateClient(HttpMessageHandler handler)
    {
        var settings = Options.Create(new PayFastSettings
        {
            MerchantId = "10043122",
            MerchantKey = "merchant-key",
            Passphrase = "passphrase",
            Sandbox = true,
            NotifyUrl = "https://localhost/payfast/itn",
            ReturnUrl = "https://localhost/billing",
            CancelUrl = "https://localhost/billing"
        });

        return new PayFastClient(new HttpClient(handler), settings, NullLogger<PayFastClient>.Instance);
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class QueueingHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
