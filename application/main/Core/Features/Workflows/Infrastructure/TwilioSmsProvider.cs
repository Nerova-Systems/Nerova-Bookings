using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Main.Features.Workflows.Senders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Twilio Programmable Messaging adapter. POSTs application/x-www-form-urlencoded to
///     <c>/Accounts/{AccountSid}/Messages.json</c> with HTTP basic auth (AccountSid : AuthToken).
///     <para>
///         Uses the named HttpClient <see cref="HttpClientName" />. When credentials are missing
///         the provider returns <see cref="SmsResult.NotConfigured" /> and logs once per call so
///         the workflow tick is not blocked in dev / unconfigured environments.
///     </para>
/// </summary>
public sealed class TwilioSmsProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<TwilioOptions> options,
    ILogger<TwilioSmsProvider> logger
) : ISmsProvider
{
    public const string HttpClientName = "twilio-sms";

    private readonly TwilioOptions _options = options.Value;

    public async Task<SmsResult> SendAsync(string toE164, string body, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            logger.LogWarning("Twilio SMS provider not configured; skipping SMS to {Recipient}.", toE164);
            return SmsResult.NotConfigured("Twilio credentials missing");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);

        var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/Accounts/{_options.AccountSid}/Messages.json";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("To", toE164),
            new KeyValuePair<string, string>("From", _options.FromNumber),
            new KeyValuePair<string, string>("Body", body)
        });

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Twilio SMS transport error for {Recipient}", toE164);
            return SmsResult.Transient(ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Twilio SMS request timed out for {Recipient}", toE164);
            return SmsResult.Transient("Request timed out");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var messageId = TryReadString(payload, "sid") ?? string.Empty;
            return SmsResult.Sent(messageId);
        }

        var reason = $"HTTP {(int)response.StatusCode} {payload}";

        // 408 / 429 / 5xx are transient; everything else (e.g. 401 bad creds, 400 bad number) is permanent.
        if (response.StatusCode == HttpStatusCode.RequestTimeout
            || response.StatusCode == HttpStatusCode.TooManyRequests
            || (int)response.StatusCode >= 500)
        {
            logger.LogWarning("Twilio transient failure for {Recipient}: {Reason}", toE164, reason);
            return SmsResult.Transient(reason);
        }

        logger.LogError("Twilio permanent failure for {Recipient}: {Reason}", toE164, reason);
        return SmsResult.Permanent(reason);
    }

    private static string? TryReadString(string payload, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall through — return null so the caller logs the raw payload.
        }

        return null;
    }
}
