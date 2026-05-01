using System.Net.Http.Headers;
using System.Text;

namespace Main.Features.Appointments;

public interface ITwilioWhatsAppClient
{
    Task SendAsync(string toPhone, string message, CancellationToken cancellationToken);
}

public sealed class TwilioWhatsAppClient(IHttpClientFactory httpClientFactory) : ITwilioWhatsAppClient
{
    public async Task SendAsync(string toPhone, string message, CancellationToken cancellationToken)
    {
        var accountSid = GetRequiredEnvironmentVariable("TWILIO_ACCOUNT_SID");
        var from = GetRequiredEnvironmentVariable("TWILIO_WHATSAPP_FROM");
        var to = toPhone.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? toPhone : $"whatsapp:{toPhone}";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(accountSid)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = from.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? from : $"whatsapp:{from}",
                ["To"] = to,
                ["Body"] = message
            })
        };
        AddBasicAuthorization(request, accountSid);

        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? "Twilio WhatsApp request failed." : body);
    }

    private static void AddBasicAuthorization(HttpRequestMessage request, string accountSid)
    {
        var authToken = GetRequiredEnvironmentVariable("TWILIO_AUTH_TOKEN");
        var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
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
