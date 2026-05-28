using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Infrastructure;

/// <summary>
///     HTTP-backed implementation of <see cref="IWhatsAppFlowProfileSync" />. Calls the internal
///     endpoints exposed by the account SCS. The base URL is read from configuration key
///     <c>WhatsApp:AccountInternalBaseUrl</c>; the shared API key from <c>WhatsApp:InternalApiKey</c>.
/// </summary>
public sealed class HttpWhatsAppFlowProfileSync(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HttpWhatsAppFlowProfileSync> logger
) : IWhatsAppFlowProfileSync
{
    public const string HttpClientName = "account-internal";

    public async Task<WhatsAppFlowProfile?> GetByTenantId(TenantId tenantId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        if (client is null) return null;

        var url = $"api/whatsapp/internal/profile?tenantId={tenantId.Value}";
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Account profile sync returned HTTP {Status} for tenant {TenantId}", (int)response.StatusCode, tenantId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<WhatsAppFlowProfile>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account profile sync failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    public async Task<WhatsAppFlowProfile?> GetByPhoneNumberId(string phoneNumberId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        if (client is null) return null;

        var url = $"api/whatsapp/internal/profile/by-phone-number/{Uri.EscapeDataString(phoneNumberId)}";
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Account profile sync returned HTTP {Status} for phone number {PhoneNumberId}", (int)response.StatusCode, phoneNumberId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<WhatsAppFlowProfile>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account profile sync failed for phone number {PhoneNumberId}", phoneNumberId);
            return null;
        }
    }

    public async Task<bool> UpdateFlowStatus(TenantId tenantId, string flowId, string status, string? generatedFlowJson, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        if (client is null) return false;

        var payload = new InternalFlowStatusUpdate(flowId, status, generatedFlowJson);
        var url = $"api/whatsapp/internal/flow-status?tenantId={tenantId.Value}";

        try
        {
            var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Account flow-status update returned HTTP {Status} for tenant {TenantId}", (int)response.StatusCode, tenantId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account flow-status update failed for tenant {TenantId}", tenantId);
            return false;
        }
    }

    private HttpClient? CreateClient()
    {
        var baseUrl = configuration["WhatsApp:AccountInternalBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("WhatsApp:AccountInternalBaseUrl is not configured; cross-SCS sync disabled.");
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

        var apiKey = configuration["WhatsApp:InternalApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        }

        return client;
    }

    [PublicAPI]
    private sealed record InternalFlowStatusUpdate(string FlowId, string Status, string? GeneratedFlowJson);
}
