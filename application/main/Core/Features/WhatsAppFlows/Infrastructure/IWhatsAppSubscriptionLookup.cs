using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Infrastructure;

/// <summary>
///     Cross-SCS lookup for the current tenant's subscription plan. Mirrors the
///     <see cref="IWhatsAppFlowProfileSync" /> HTTP pattern — backed by an internal endpoint on
///     the account SCS. Returns the raw plan string (<c>Basis</c> / <c>Standard</c> /
///     <c>Premium</c>) or <c>null</c> when the tenant has no active subscription.
/// </summary>
public interface IWhatsAppSubscriptionLookup
{
    Task<string?> GetSubscriptionPlanAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public sealed class HttpWhatsAppSubscriptionLookup(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HttpWhatsAppSubscriptionLookup> logger
) : IWhatsAppSubscriptionLookup
{
    public const string HttpClientName = "account-internal-subscription";

    public async Task<string?> GetSubscriptionPlanAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        if (client is null) return null;

        var url = $"api/whatsapp/internal/subscription?tenantId={tenantId.Value}";
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Account subscription lookup returned HTTP {Status} for tenant {TenantId}", (int)response.StatusCode, tenantId);
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<SubscriptionLookupResponse>(cancellationToken);
            return dto?.Plan;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account subscription lookup failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    private HttpClient? CreateClient()
    {
        var baseUrl = configuration["WhatsApp:AccountInternalBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("WhatsApp:AccountInternalBaseUrl is not configured; subscription lookup disabled.");
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
    public sealed record SubscriptionLookupResponse(string? Plan);
}
