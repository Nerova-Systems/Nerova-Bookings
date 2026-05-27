using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppFlows.Infrastructure;

[PublicAPI]
public sealed record CreateFlowResponse(string FlowId);

[PublicAPI]
public sealed record FlowPreviewResponse(string PreviewUrl, DateTimeOffset ExpiresAt);

public interface IMetaFlowsApiClient
{
    Task<Result<CreateFlowResponse>> CreateFlowAsync(string wabaId, string name, string accessToken, CancellationToken cancellationToken);

    Task<Result> UploadFlowAssetAsync(string flowId, string flowJson, string accessToken, CancellationToken cancellationToken);

    Task<Result> PublishFlowAsync(string flowId, string accessToken, CancellationToken cancellationToken);

    Task<Result> DeprecateFlowAsync(string flowId, string accessToken, CancellationToken cancellationToken);

    Task<Result<FlowPreviewResponse>> GetFlowPreviewUrlAsync(string flowId, string accessToken, CancellationToken cancellationToken);

    Task<Result> UploadEncryptionPublicKeyAsync(string phoneNumberId, string publicKeyPem, string accessToken, CancellationToken cancellationToken);
}

/// <summary>
///     Thin Meta Graph v21 adapter for WhatsApp Flows. Each call uses the per-request access
///     token (supplied by the caller) rather than configured credentials, so the same client
///     can serve every tenant.
/// </summary>
public sealed class MetaFlowsApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<MetaFlowsApiClient> logger
) : IMetaFlowsApiClient
{
    public const string HttpClientName = "meta-graph";

    private string BaseUrl => configuration["WhatsApp:GraphApiBaseUrl"] ?? "https://graph.facebook.com/v21.0/";

    public async Task<Result<CreateFlowResponse>> CreateFlowAsync(string wabaId, string name, string accessToken, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        var url = $"{wabaId}/flows";
        var payload = new
        {
            name,
            categories = new[] { "OTHER" }
        };

        using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Meta CreateFlow failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
            return Result<CreateFlowResponse>.BadRequest($"Meta CreateFlow failed: HTTP {(int)response.StatusCode}");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
            return new CreateFlowResponse(id);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Meta CreateFlow returned malformed JSON: {Body}", raw);
            return Result<CreateFlowResponse>.BadRequest("Meta CreateFlow returned malformed JSON");
        }
    }

    public async Task<Result> UploadFlowAssetAsync(string flowId, string flowJson, string accessToken, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        var url = $"{flowId}/assets";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("FLOW_JSON"), "asset_type");
        content.Add(new StringContent("flow.json"), "name");
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(flowJson));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "flow.json");

        using var response = await client.PostAsync(url, content, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Meta UploadFlowAsset failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
        return Result.BadRequest($"Meta UploadFlowAsset failed: HTTP {(int)response.StatusCode}");
    }

    public async Task<Result> PublishFlowAsync(string flowId, string accessToken, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        using var response = await client.PostAsync($"{flowId}/publish", content: null, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Meta PublishFlow failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
        return Result.BadRequest($"Meta PublishFlow failed: HTTP {(int)response.StatusCode}");
    }

    public async Task<Result> DeprecateFlowAsync(string flowId, string accessToken, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        using var response = await client.PostAsync($"{flowId}/deprecate", content: null, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Meta DeprecateFlow failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
        return Result.BadRequest($"Meta DeprecateFlow failed: HTTP {(int)response.StatusCode}");
    }

    public async Task<Result<FlowPreviewResponse>> GetFlowPreviewUrlAsync(string flowId, string accessToken, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        using var response = await client.GetAsync($"{flowId}?fields=preview.invalidate(false)", cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Meta GetFlowPreview failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
            return Result<FlowPreviewResponse>.BadRequest($"Meta GetFlowPreview failed: HTTP {(int)response.StatusCode}");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var preview = doc.RootElement.GetProperty("preview");
            var previewUrl = preview.GetProperty("preview_url").GetString() ?? string.Empty;
            var expiresAtStr = preview.TryGetProperty("expires_at", out var exp) ? exp.GetString() : null;
            var expiresAt = DateTimeOffset.TryParse(expiresAtStr, out var parsed) ? parsed : DateTimeOffset.UtcNow.AddHours(24);
            return new FlowPreviewResponse(previewUrl, expiresAt);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Meta GetFlowPreview returned malformed JSON: {Body}", raw);
            return Result<FlowPreviewResponse>.BadRequest("Meta GetFlowPreview returned malformed JSON");
        }
    }

    public async Task<Result> UploadEncryptionPublicKeyAsync(string phoneNumberId, string publicKeyPem, string accessToken, CancellationToken cancellationToken)
    {
        var client = CreateClient(accessToken);
        var url = $"{phoneNumberId}/whatsapp_business_encryption";
        using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("business_public_key", publicKeyPem)
            }
        );
        using var response = await client.PostAsync(url, content, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Meta UploadEncryptionPublicKey failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
        return Result.BadRequest($"Meta UploadEncryptionPublicKey failed: HTTP {(int)response.StatusCode}");
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
