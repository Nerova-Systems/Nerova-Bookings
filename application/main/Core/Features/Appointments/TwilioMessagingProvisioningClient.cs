using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SharedKernel.SinglePageApp;

namespace Main.Features.Appointments;

public interface ITwilioMessagingProvisioningClient
{
    Task<TwilioSubaccountProvisioningResult> CreateSubaccountAsync(string friendlyName, CancellationToken cancellationToken);
    Task<TwilioPhoneNumberClaimResult> ClaimSouthAfricanNumberAsync(string accountSid, CancellationToken cancellationToken);
}

public sealed class TwilioMessagingProvisioningClient(IHttpClientFactory httpClientFactory) : ITwilioMessagingProvisioningClient
{
    private static readonly Uri DefaultBaseUrl = new("https://api.twilio.com");

    public async Task<TwilioSubaccountProvisioningResult> CreateSubaccountAsync(string friendlyName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(GetBaseUrl(), "/2010-04-01/Accounts.json"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FriendlyName"] = friendlyName
            })
        };
        AddMasterAuthorization(request);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new TwilioSubaccountProvisioningResult(ReadString(json, "sid") ?? string.Empty, ReadString(json, "status") ?? "active");
    }

    public async Task<TwilioPhoneNumberClaimResult> ClaimSouthAfricanNumberAsync(string accountSid, CancellationToken cancellationToken)
    {
        using var search = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(GetBaseUrl(), $"/2010-04-01/Accounts/{Uri.EscapeDataString(accountSid)}/AvailablePhoneNumbers/ZA/Local.json?SmsEnabled=true&PageSize=1")
        );
        AddMasterAuthorization(search);
        var searchResponse = await httpClientFactory.CreateClient().SendAsync(search, cancellationToken);
        await EnsureSuccessAsync(searchResponse, cancellationToken);
        var searchJson = await searchResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var availableNumber = ReadFirstAvailablePhoneNumber(searchJson);
        if (string.IsNullOrWhiteSpace(availableNumber))
        {
            throw new InvalidOperationException("No South African Twilio phone numbers are currently available for assignment.");
        }

        var webhookUrl = BuildWebhookUrl("/api/main/webhooks/twilio/messaging");
        using var claim = new HttpRequestMessage(HttpMethod.Post, new Uri(GetBaseUrl(), $"/2010-04-01/Accounts/{Uri.EscapeDataString(accountSid)}/IncomingPhoneNumbers.json"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["PhoneNumber"] = availableNumber,
                ["SmsUrl"] = webhookUrl,
                ["SmsMethod"] = "POST",
                ["StatusCallback"] = BuildWebhookUrl("/api/main/webhooks/twilio/status"),
                ["StatusCallbackMethod"] = "POST"
            })
        };
        AddMasterAuthorization(claim);
        var claimResponse = await httpClientFactory.CreateClient().SendAsync(claim, cancellationToken);
        await EnsureSuccessAsync(claimResponse, cancellationToken);
        var claimJson = await claimResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new TwilioPhoneNumberClaimResult(
            ReadString(claimJson, "sid") ?? string.Empty,
            ReadString(claimJson, "phone_number") ?? availableNumber,
            true,
            false,
            webhookUrl
        );
    }

    private static void AddMasterAuthorization(HttpRequestMessage request)
    {
        var accountSid = GetRequiredEnvironmentVariable("TWILIO_MASTER_ACCOUNT_SID", "TWILIO_ACCOUNT_SID");
        var authToken = GetRequiredEnvironmentVariable("TWILIO_MASTER_AUTH_TOKEN", "TWILIO_AUTH_TOKEN");
        var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? "Twilio messaging provisioning request failed." : body);
    }

    private static string? ReadFirstAvailablePhoneNumber(JsonElement json)
    {
        if (!json.TryGetProperty("available_phone_numbers", out var numbers) || numbers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return numbers.EnumerateArray().Select(number => ReadString(number, "phone_number")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? ReadString(JsonElement json, string property)
    {
        return json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static Uri GetBaseUrl()
    {
        var configured = Environment.GetEnvironmentVariable("TWILIO_SERVER_URL");
        return string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : new Uri(configured, UriKind.Absolute);
    }

    private static string BuildWebhookUrl(string path)
    {
        var publicUrl = Environment.GetEnvironmentVariable(SinglePageAppConfiguration.PublicUrlKey);
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            throw new InvalidOperationException($"{SinglePageAppConfiguration.PublicUrlKey} is not configured.");
        }

        return $"{publicUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static string GetRequiredEnvironmentVariable(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value) && value != "not-configured")
            {
                return value;
            }
        }

        throw new InvalidOperationException($"{keys[0]} is not configured.");
    }
}

public sealed record TwilioSubaccountProvisioningResult(string AccountSid, string Status);
public sealed record TwilioPhoneNumberClaimResult(string PhoneNumberSid, string PhoneNumber, bool SmsCapable, bool WhatsAppCapable, string WebhookUrl);
