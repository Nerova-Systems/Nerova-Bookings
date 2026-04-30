using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Main.Features.Appointments;

public interface ITwilioVerifyClient
{
    Task<TwilioVerificationStarted> StartVerificationAsync(string phone, CancellationToken cancellationToken);
    Task<bool> CheckVerificationAsync(string phone, string code, CancellationToken cancellationToken);
}

public sealed class TwilioVerifyClient(IHttpClientFactory httpClientFactory) : ITwilioVerifyClient
{
    public async Task<TwilioVerificationStarted> StartVerificationAsync(string phone, CancellationToken cancellationToken)
    {
        var serviceSid = GetRequiredEnvironmentVariable("TWILIO_VERIFY_SERVICE_SID");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"https://verify.twilio.com/v2/Services/{Uri.EscapeDataString(serviceSid)}/Verifications")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phone,
                ["Channel"] = "sms"
            })
        };
        AddBasicAuthorization(message);

        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new TwilioVerificationStarted(
            json.TryGetProperty("sid", out var sid) ? sid.GetString() ?? string.Empty : string.Empty,
            json.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty
        );
    }

    public async Task<bool> CheckVerificationAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var serviceSid = GetRequiredEnvironmentVariable("TWILIO_VERIFY_SERVICE_SID");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"https://verify.twilio.com/v2/Services/{Uri.EscapeDataString(serviceSid)}/VerificationCheck")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phone,
                ["Code"] = code
            })
        };
        AddBasicAuthorization(message);

        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.TryGetProperty("status", out var status) && status.GetString() == "approved";
    }

    private static void AddBasicAuthorization(HttpRequestMessage request)
    {
        var accountSid = GetRequiredEnvironmentVariable("TWILIO_ACCOUNT_SID");
        var authToken = GetRequiredEnvironmentVariable("TWILIO_AUTH_TOKEN");
        var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? "Twilio Verify request failed." : body);
    }

    private static string GetRequiredEnvironmentVariable(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value) || value == "not-configured")
        {
            throw new InvalidOperationException($"{key} is not configured.");
        }
        return value;
    }
}

public sealed record TwilioVerificationStarted(string Sid, string Status);
