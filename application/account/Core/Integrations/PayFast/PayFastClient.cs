using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Account.Integrations.PayFast;

public sealed class PayFastClient(HttpClient httpClient, IOptions<PayFastSettings> options, ILogger<PayFastClient> logger) : IPayFastClient
{
    private PayFastSettings Settings => options.Value;

    private string BaseUrl => Settings.Sandbox
        ? "https://sandbox.payfast.co.za"
        : "https://www.payfast.co.za";

    // Recurring Billing API host is always api.payfast.co.za. Sandbox mode is signalled by
    // appending ?testing=true to each request, NOT by changing the host.
    private const string ApiBaseUrl = "https://api.payfast.co.za";

    private string TestingQuery => Settings.Sandbox ? "?testing=true" : string.Empty;

    public async Task<string?> ProcessOnsitePaymentAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        try
        {
            var formContent = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync($"{BaseUrl}/onsite/process", formContent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("PayFast onsite/process failed with status {StatusCode}: {Body}", response.StatusCode, body);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OnsiteProcessResponse>(cancellationToken);
            return result?.Uuid;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout calling PayFast onsite/process");
            return null;
        }
    }

    public async Task<bool> ChargeTokenAsync(string token, decimal amountRand, string itemName, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = FormatPayFastTimestamp(DateTimeOffset.UtcNow);
            var amountInCents = ((int)Math.Round(amountRand * 100m, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

            // PayFast Recurring Billing API signs all headers + body params alphabetically. The body
            // is sent as URL-encoded form data (Content-Type: application/x-www-form-urlencoded), even
            // though the docs sometimes show JSON examples — the signature scheme is form-style.
            var signedFields = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "amount", amountInCents },
                { "item_name", itemName },
                { "merchant-id", Settings.MerchantId },
                { "timestamp", timestamp },
                { "version", "v1" }
            };
            var signature = PayFastSignature.GenerateApiSignature(signedFields, Settings.Passphrase);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/subscriptions/{token}/adhoc{TestingQuery}");
            request.Headers.Add("merchant-id", Settings.MerchantId);
            request.Headers.Add("version", "v1");
            request.Headers.Add("timestamp", timestamp);
            request.Headers.Add("signature", signature);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "amount", amountInCents },
                { "item_name", itemName }
            });

            var response = await httpClient.SendAsync(request, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var snippet = rawBody.Length > 500 ? rawBody[..500] : rawBody;
                logger.LogError("PayFast adhoc charge HTTP {StatusCode} for token {Token}. Body: {Body}", response.StatusCode, token, snippet);
                return false;
            }

            // PayFast returns HTTP 200 even when the charge itself was declined or refused. The actual
            // outcome lives inside the JSON envelope: { "code": 200, "status": "success",
            // "data": { "response": true|false, "message": "..." } }. Treat anything other than
            // data.response === true as a hard failure so the caller does not apply plan/state changes.
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var data = doc.RootElement.TryGetProperty("data", out var d) ? d : default;
                var charged = data.ValueKind == JsonValueKind.Object &&
                              data.TryGetProperty("response", out var resp) &&
                              resp.ValueKind == JsonValueKind.True;

                if (!charged)
                {
                    var message = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                        ? m.GetString()
                        : "no message";
                    logger.LogError("PayFast adhoc charge declined for token {Token} ({Amount} cents). Message: {Message}. Body: {Body}", token, amountInCents, message, rawBody);
                    return false;
                }

                return true;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "PayFast adhoc charge returned non-JSON body for token {Token}: {Body}", token, rawBody);
                return false;
            }
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout calling PayFast adhoc charge for token {Token}", token);
            return false;
        }
    }

    public async Task<bool> CancelSubscriptionAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = FormatPayFastTimestamp(DateTimeOffset.UtcNow);
            var signedFields = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "merchant-id", Settings.MerchantId },
                { "timestamp", timestamp },
                { "version", "v1" }
            };
            var signature = PayFastSignature.GenerateApiSignature(signedFields, Settings.Passphrase);

            var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/subscriptions/{token}/cancel{TestingQuery}");
            request.Headers.Add("merchant-id", Settings.MerchantId);
            request.Headers.Add("version", "v1");
            request.Headers.Add("timestamp", timestamp);
            request.Headers.Add("signature", signature);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("PayFast cancel subscription failed for token {Token} with status {StatusCode}: {Body}", token, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout calling PayFast cancel for token {Token}", token);
            return false;
        }
    }

    public string GetUpdateCardUrl(string token)
    {
        return $"{BaseUrl}/eng/recurring/update/{token}";
    }

    // PayFast expects ISO 8601 with second precision and timezone offset (no fractional seconds).
    private static string FormatPayFastTimestamp(DateTimeOffset utcNow)
        => utcNow.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private sealed record OnsiteProcessResponse(string Uuid);
}
