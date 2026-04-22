using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Account.Integrations.PayFast;

public sealed class PayFastClient(HttpClient httpClient, IOptions<PayFastSettings> options, ILogger<PayFastClient> logger)
{
    private PayFastSettings Settings => options.Value;

    private string BaseUrl => Settings.Sandbox
        ? "https://sandbox.payfast.co.za"
        : "https://www.payfast.co.za";

    private string ApiBaseUrl => Settings.Sandbox
        ? "https://sandbox.payfast.co.za"
        : "https://api.payfast.co.za";

    public async Task<string?> ProcessOnsitePaymentAsync(SortedDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{BaseUrl}/onsite/process", parameters, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayFast onsite/process failed with status {StatusCode}", response.StatusCode);
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

    public async Task<bool> ChargeSubscriptionAsync(string token, decimal amount, string itemName, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("o");
            var signature = PayFastSignature.GenerateApiSignature(Settings.MerchantId, Settings.Passphrase, timestamp);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/subscriptions/{token}/charge");
            request.Headers.Add("merchant-id", Settings.MerchantId);
            request.Headers.Add("version", "v1");
            request.Headers.Add("timestamp", timestamp);
            request.Headers.Add("signature", signature);
            request.Content = JsonContent.Create(new { amount, item_name = itemName });

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayFast charge subscription failed for token {Token} with status {StatusCode}", token, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout calling PayFast charge for token {Token}", token);
            return false;
        }
    }

    public async Task<bool> CancelSubscriptionAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("o");
            var signature = PayFastSignature.GenerateApiSignature(Settings.MerchantId, Settings.Passphrase, timestamp);

            var request = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/subscriptions/{token}/cancel");
            request.Headers.Add("merchant-id", Settings.MerchantId);
            request.Headers.Add("version", "v1");
            request.Headers.Add("timestamp", timestamp);
            request.Headers.Add("signature", signature);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayFast cancel subscription failed for token {Token} with status {StatusCode}", token, response.StatusCode);
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

    private sealed record OnsiteProcessResponse(string Uuid);
}
