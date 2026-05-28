using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Main.Features.Workflows.Senders;
using Microsoft.Extensions.Options;

namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Meta WhatsApp Business Cloud API adapter. POSTs a JSON template message to
///     <c>/{phone-number-id}/messages</c> with a Bearer access token.
///     <para>
///         Variables are positional in WhatsApp templates (<c>{{1}}</c>, <c>{{2}}</c>, ...).
///         The provided dictionary is sorted by integer key when numeric, otherwise by ordinal
///         key — callers should pass <c>{"1": ..., "2": ...}</c> for predictable ordering.
///         When credentials are missing the provider returns <see cref="WhatsAppResult.NotConfigured" />
///         and logs once per call so the workflow tick is not blocked.
///     </para>
/// </summary>
public sealed class MetaWhatsAppProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MetaWhatsAppOptions> options,
    ILogger<MetaWhatsAppProvider> logger
) : IWhatsAppProvider
{
    public const string HttpClientName = "meta-whatsapp";

    private readonly MetaWhatsAppOptions _options = options.Value;

    public async Task<WhatsAppResult> SendAsync(
        string toE164,
        string templateName,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken
    )
    {
        if (!_options.IsConfigured)
        {
            logger.LogWarning("Meta WhatsApp provider not configured; skipping message to {Recipient}.", toE164);
            return WhatsAppResult.NotConfigured("Meta WhatsApp credentials missing");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);

        var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/{_options.PhoneNumberId}/messages";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        var bodyParameters = OrderedParameters(variables)
            .Select(value => new { type = "text", text = value })
            .Cast<object>()
            .ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            to = toE164.TrimStart('+'),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = _options.DefaultLanguageCode },
                components = bodyParameters.Length == 0
                    ? Array.Empty<object>()
                    : [new { type = "body", parameters = bodyParameters }]
            }
        };

        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Meta WhatsApp transport error for {Recipient}", toE164);
            return WhatsAppResult.Transient(ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Meta WhatsApp request timed out for {Recipient}", toE164);
            return WhatsAppResult.Transient("Request timed out");
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var messageId = TryReadMessageId(raw) ?? string.Empty;
            return WhatsAppResult.Sent(messageId);
        }

        var reason = $"HTTP {(int)response.StatusCode} {raw}";

        if (response.StatusCode == HttpStatusCode.RequestTimeout
            || response.StatusCode == HttpStatusCode.TooManyRequests
            || (int)response.StatusCode >= 500)
        {
            logger.LogWarning("Meta WhatsApp transient failure for {Recipient}: {Reason}", toE164, reason);
            return WhatsAppResult.Transient(reason);
        }

        logger.LogError("Meta WhatsApp permanent failure for {Recipient}: {Reason}", toE164, reason);
        return WhatsAppResult.Permanent(reason);
    }

    /// <summary>
    ///     WhatsApp templates use positional placeholders ({{1}}, {{2}}, ...). Sort numerically
    ///     when all keys parse as ints; otherwise fall back to ordinal sort for stability.
    /// </summary>
    private static IEnumerable<string> OrderedParameters(IReadOnlyDictionary<string, string> variables)
    {
        if (variables.Count == 0) return [];

        var allNumeric = variables.Keys.All(k => int.TryParse(k, out _));
        return allNumeric
            ? variables.OrderBy(kvp => int.Parse(kvp.Key)).Select(kvp => kvp.Value)
            : variables.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => kvp.Value);
    }

    private static string? TryReadMessageId(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall through — caller logs the raw payload.
        }

        return null;
    }
}
