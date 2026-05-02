using System.Net.Http.Headers;
using System.Text;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;

namespace Main.Features.Appointments;

public interface ITwilioWhatsAppClient
{
    Task SendAsync(TenantId tenantId, string toPhone, string message, CancellationToken cancellationToken);
}

public sealed class TwilioWhatsAppClient(IHttpClientFactory httpClientFactory, MainDbContext db) : ITwilioWhatsAppClient
{
    public async Task SendAsync(TenantId tenantId, string toPhone, string message, CancellationToken cancellationToken)
    {
        var profile = await db.TenantMessagingProfiles.AsNoTracking().FirstOrDefaultAsync(
            profile => profile.TenantId == tenantId && profile.AppSlug == "whatsapp",
            cancellationToken
        );
        if (profile?.WhatsAppApprovalStatus != "Approved")
        {
            throw new InvalidOperationException("WhatsApp sender is not approved for this tenant.");
        }

        var sender = await db.TenantPhoneNumberAssignments.AsNoTracking()
            .Where(number => number.TenantId == tenantId && number.MessagingProfileId == profile.Id && number.AssignmentStatus == "Assigned")
            .FirstOrDefaultAsync(cancellationToken);
        if (sender is null)
        {
            throw new InvalidOperationException("No tenant WhatsApp sender number is assigned.");
        }

        var accountSid = profile.TwilioSubaccountSid;
        if (string.IsNullOrWhiteSpace(accountSid))
        {
            throw new InvalidOperationException("Tenant Twilio subaccount is not configured.");
        }

        var from = sender.PhoneNumber;
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
        var masterAccountSid = GetRequiredEnvironmentVariable("TWILIO_MASTER_ACCOUNT_SID", "TWILIO_ACCOUNT_SID");
        var authToken = GetRequiredEnvironmentVariable("TWILIO_MASTER_AUTH_TOKEN", "TWILIO_AUTH_TOKEN");
        var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{masterAccountSid}:{authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
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
