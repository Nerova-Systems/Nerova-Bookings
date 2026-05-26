using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Main.Features.Webhooks.Domain;

namespace Main.Features.Webhooks.Infrastructure;

/// <summary>
///     POSTs a single <see cref="WebhookDelivery" /> payload to its target URL and applies the
///     backoff schedule from <see cref="WebhookBackoff" /> on failure. Extracted from the TickerQ
///     job so the HTTP behaviour is unit-testable without standing up the worker host.
/// </summary>
public sealed class WebhookDeliveryProcessor(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<WebhookDeliveryProcessor> logger
)
{
    public const string HttpClientName = "webhook-delivery";

    /// <summary>Hard cap on outbound delivery attempts to keep slow targets from blocking the poller.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public async Task ProcessAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Status is not (WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.Failed))
        {
            return;
        }

        var attemptAt = timeProvider.GetUtcNow();
        using var request = BuildRequest(delivery);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            var body = await SafeReadAsync(response, cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.RecordSuccess(statusCode, body, attemptAt);
                return;
            }

            var nextAttemptAt = ComputeNextAttemptAt(delivery.AttemptCount + 1, attemptAt);
            delivery.RecordFailure(statusCode, body, attemptAt, nextAttemptAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Worker shutdown: do not consume the attempt. Surface so TickerQ can stop cleanly.
                throw;
            }

            logger.LogWarning(ex, "Webhook delivery {DeliveryId} attempt {Attempt} failed", delivery.Id, delivery.AttemptCount + 1);
            var nextAttemptAt = ComputeNextAttemptAt(delivery.AttemptCount + 1, attemptAt);
            delivery.RecordFailure(null, ex.Message, attemptAt, nextAttemptAt);
        }
    }

    private static HttpRequestMessage BuildRequest(WebhookDelivery delivery)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, delivery.RequestUrl)
        {
            Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json")
        };

        // Headers are applied verbatim so signature/user-agent match what was persisted on enqueue.
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(delivery.RequestHeadersJson)
                      ?? new Dictionary<string, string>();
        foreach (var (name, value) in headers)
        {
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                continue;
            }

            request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }

    private static DateTimeOffset? ComputeNextAttemptAt(int nextAttemptNumber, DateTimeOffset attemptAt)
    {
        // nextAttemptNumber == AttemptCount after this failure is recorded. Backoff returns the
        // delay to wait BEFORE attempting again. Null → dead-letter.
        var delay = WebhookBackoff.GetDelayAfterAttempt(nextAttemptNumber);
        return attemptAt + delay;
    }

    private static async Task<string?> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
