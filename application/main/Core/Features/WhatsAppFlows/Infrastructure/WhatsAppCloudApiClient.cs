using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace Main.Features.WhatsAppFlows.Infrastructure;

/// <summary>
///     Thrown when the Meta WhatsApp Cloud API returns a non-success status. The HTTP code +
///     response body are preserved so callers can log/inspect; callers should treat this as a
///     transient infrastructure failure and decide whether to retry.
/// </summary>
[PublicAPI]
public sealed class WhatsAppCloudApiException(int statusCode, string responseBody)
    : Exception($"WhatsApp Cloud API returned HTTP {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}

public interface IWhatsAppCloudApiClient
{
    /// <summary>
    ///     Sends a free-form text message via the Cloud API (<c>type=text</c>). Only valid inside
    ///     a 24-hour customer service window; outside it Meta will reject the call.
    /// </summary>
    Task SendTextMessageAsync(string phoneNumberId, string accessToken, string toWaId, string body, CancellationToken cancellationToken);

    /// <summary>
    ///     Sends a pre-approved template message (<c>type=template</c>). Always valid as the
    ///     first message to a user; required for the post-flow confirmation when the 24-hour
    ///     window has elapsed.
    /// </summary>
    Task SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string toWaId,
        string templateName,
        string languageCode,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken
    );
}

/// <summary>
///     Thin Meta WhatsApp Cloud API adapter (Graph v21). Per-tenant access token + phone-number
///     id are passed in by callers so a single registered client serves every tenant.
/// </summary>
public sealed class WhatsAppCloudApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WhatsAppCloudApiClient> logger
) : IWhatsAppCloudApiClient
{
    public const string HttpClientName = "whatsapp-cloud-api";

    private string BaseUrl => configuration["WhatsApp:GraphApiBaseUrl"] ?? "https://graph.facebook.com/v21.0/";

    public async Task SendTextMessageAsync(string phoneNumberId, string accessToken, string toWaId, string body, CancellationToken cancellationToken)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toWaId,
            type = "text",
            text = new { body }
        };

        await SendAsync(phoneNumberId, accessToken, payload, cancellationToken);
    }

    public async Task SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string toWaId,
        string templateName,
        string languageCode,
        IReadOnlyList<string> bodyParameters,
        CancellationToken cancellationToken)
    {
        // Cloud API requires the components array to be present even when empty; supplying it as
        // an empty array (rather than omitting it) is the documented shape for parameterless templates.
        var components = bodyParameters.Count == 0
            ? Array.Empty<object>()
            : new object[]
            {
                new
                {
                    type = "body",
                    parameters = bodyParameters.Select(p => new { type = "text", text = p }).ToArray()
                }
            };

        var payload = new
        {
            messaging_product = "whatsapp",
            to = toWaId,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components
            }
        };

        await SendAsync(phoneNumberId, accessToken, payload, cancellationToken);
    }

    private async Task SendAsync(string phoneNumberId, string accessToken, object payload, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        var url = $"{phoneNumberId}/messages";

        using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
        if (response.IsSuccessStatusCode) return;

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("WhatsApp Cloud API send failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
        throw new WhatsAppCloudApiException((int)response.StatusCode, raw);
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
