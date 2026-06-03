using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace Main.Features.Payments.Paystack;

/// <summary>Result of a successful Paystack <c>/transaction/initialize</c> call.</summary>
[PublicAPI]
public sealed record PaystackPaymentLink(string AuthorizationUrl, string AccessCode, string Reference);

public interface IPaystackPaymentLinkService
{
    /// <summary>
    ///     Initialises a hosted Paystack checkout for a booking deposit. Routes the funds to the
    ///     tenant via their connected <c>subaccount</c> (Paystack splits the payment automatically).
    ///     Returns <c>null</c> on failure; callers should log + abort the post-flow dispatch.
    /// </summary>
    Task<PaystackPaymentLink?> CreatePaymentLinkAsync(
        string subaccountCode,
        long amountMinorUnits,
        string currency,
        string customerEmail,
        string reference,
        string? callbackUrl,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken
    );
}

/// <summary>
///     Booking-payment-specific Paystack client. Lives in <c>main</c> SCS because booking-level
///     payments are a main-SCS concern (the <c>account</c> SCS's <c>PaystackClient</c>
///     handles subscription billing, which is a different domain with different references/metadata).
/// </summary>
public sealed class PaystackPaymentLinkService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PaystackPaymentLinkService> logger
) : IPaystackPaymentLinkService
{
    public const string HttpClientName = "paystack-payments";

    private string SecretKey => configuration["Paystack:SecretKey"] ?? string.Empty;

    private string BaseUrl => configuration["Paystack:BaseUrl"] ?? "https://api.paystack.co/";

    public async Task<PaystackPaymentLink?> CreatePaymentLinkAsync(
        string subaccountCode,
        long amountMinorUnits,
        string currency,
        string customerEmail,
        string reference,
        string? callbackUrl,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            logger.LogWarning("Paystack:SecretKey is not configured. Cannot initialise payment link.");
            return null;
        }

        var payload = new Dictionary<string, object?>
        {
            ["email"] = customerEmail,
            ["amount"] = amountMinorUnits,
            ["currency"] = currency,
            ["reference"] = reference,
            ["subaccount"] = subaccountCode
        };

        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            payload["callback_url"] = callbackUrl;
        }

        if (metadata is { Count: > 0 })
        {
            payload["metadata"] = metadata;
        }

        var client = CreateClient();

        using var response = await client.PostAsJsonAsync("transaction/initialize", payload, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Paystack initialize failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var data = doc.RootElement.GetProperty("data");
            var authUrl = data.GetProperty("authorization_url").GetString() ?? string.Empty;
            var accessCode = data.GetProperty("access_code").GetString() ?? string.Empty;
            var returnedRef = data.GetProperty("reference").GetString() ?? reference;
            return new PaystackPaymentLink(authUrl, accessCode, returnedRef);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Paystack initialize returned malformed JSON: {Body}", raw);
            return null;
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Paystack initialize response missing expected fields: {Body}", raw);
            return null;
        }
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress ??= new Uri(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
        return client;
    }
}
