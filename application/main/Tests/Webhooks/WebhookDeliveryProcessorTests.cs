using System.Net;
using FluentAssertions;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Webhooks;

public sealed class WebhookDeliveryProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessAsync_When2xx_ShouldRecordSuccessAndClearNextAttempt()
    {
        var delivery = NewDelivery();
        var processor = BuildProcessor(new StubHandler(HttpStatusCode.OK, "{\"ok\":true}"));

        await processor.ProcessAsync(delivery, CancellationToken.None);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Succeeded);
        delivery.AttemptCount.Should().Be(1);
        delivery.ResponseStatusCode.Should().Be(200);
        delivery.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenFirstFailure_ShouldSetFailedAndScheduleRetry()
    {
        var delivery = NewDelivery();
        var processor = BuildProcessor(new StubHandler(HttpStatusCode.InternalServerError, "boom"));

        await processor.ProcessAsync(delivery, CancellationToken.None);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(1);
        delivery.ResponseStatusCode.Should().Be(500);
        delivery.NextAttemptAt.Should().Be(Now + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ProcessAsync_WhenSixthFailure_ShouldDeadLetter()
    {
        var delivery = NewDelivery();
        // Replay five prior failed attempts so AttemptCount reaches 5 before this call.
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var delay = WebhookBackoff.GetDelayAfterAttempt(attempt);
            delivery.RecordFailure(503, "tmp", Now.AddMinutes(-1), Now + (delay ?? TimeSpan.Zero));
        }

        delivery.AttemptCount.Should().Be(5);
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);

        var processor = BuildProcessor(new StubHandler(HttpStatusCode.InternalServerError, "boom"));
        await processor.ProcessAsync(delivery, CancellationToken.None);

        delivery.AttemptCount.Should().Be(6);
        delivery.Status.Should().Be(WebhookDeliveryStatus.DeadLettered);
        delivery.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenHttpRequestThrows_ShouldRecordFailureWithoutStatusCode()
    {
        var delivery = NewDelivery();
        var processor = BuildProcessor(new ThrowingHandler(new HttpRequestException("dns failure")));

        await processor.ProcessAsync(delivery, CancellationToken.None);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(1);
        delivery.ResponseStatusCode.Should().BeNull();
        delivery.ResponseBody.Should().Be("dns failure");
        delivery.NextAttemptAt.Should().Be(Now + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ProcessAsync_WhenDeliveryAlreadyTerminal_ShouldBeNoOp()
    {
        var delivery = NewDelivery();
        delivery.RecordSuccess(200, "ok", Now);
        var processor = BuildProcessor(new StubHandler(HttpStatusCode.InternalServerError, "should-not-be-called"));

        await processor.ProcessAsync(delivery, CancellationToken.None);

        delivery.AttemptCount.Should().Be(1); // unchanged
        delivery.Status.Should().Be(WebhookDeliveryStatus.Succeeded);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static WebhookDelivery NewDelivery()
    {
        return WebhookDelivery.Create(
            new TenantId(1),
            new WebhookId("wh_test"),
            WebhookEventType.Ping,
            "{\"hello\":\"world\"}",
            "https://example.test/hook",
            "{\"Content-Type\":\"application/json\"}",
            Now
        );
    }

    private static WebhookDeliveryProcessor BuildProcessor(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WebhookDeliveryProcessor.HttpClientName).Returns(_ => new HttpClient(handler));

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(Now);

        return new WebhookDeliveryProcessor(factory, timeProvider, NullLogger<WebhookDeliveryProcessor>.Instance);
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }
}
