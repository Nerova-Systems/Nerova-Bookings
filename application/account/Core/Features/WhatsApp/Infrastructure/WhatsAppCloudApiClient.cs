using System.Net.Http.Headers;
using System.Net.Http.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using SharedKernel.Cqrs;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Thrown when the Meta WhatsApp Cloud API returns a non-success status. The HTTP code and
///     response body are preserved so callers can decide whether the failure is retryable.
/// </summary>
/// <remarks>
///     Mirrors the exception type in the Main SCS (<c>Main.Features.WhatsAppFlows.Infrastructure.
///     WhatsAppCloudApiException</c>). The duplication is intentional: per the SCS isolation
///     rules, the Account SCS cannot reference Main, and there is no shared kernel module for
///     WhatsApp infrastructure types. If a third SCS ends up needing the same client, the
///     refactor should hoist these types into a SharedKernel.WhatsApp module rather than
///     cross-link SCSs.
/// </remarks>
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
    ///     Uploads the brand logo bytes via the Meta Resumable Upload API (Graph v25). Returns the
    ///     opaque upload handle that must be passed to <see cref="UpdateBusinessProfileAsync" />
    ///     via <c>profile_picture_handle</c>.
    /// </summary>
    /// <param name="appId">The Meta app id (the resumable upload endpoint is app-scoped, not WABA-scoped).</param>
    /// <param name="accessToken">Per-tenant WABA system-user token.</param>
    /// <param name="bytes">Raw image bytes (PNG or JPEG).</param>
    /// <param name="contentType">MIME type, e.g. <c>image/png</c>.</param>
    Task<Result<string>> UploadProfilePictureAsync(
        string appId,
        string accessToken,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     POSTs the supplied serialized <c>whatsapp_business_profile</c> payload (already JSON
    ///     bytes including <c>messaging_product=whatsapp</c>) to the phone-number-scoped endpoint.
    /// </summary>
    /// <param name="phoneNumberId">Meta phone number id from <c>WabaConfiguration</c>.</param>
    /// <param name="accessToken">Per-tenant WABA system-user token.</param>
    /// <param name="serializedPayload">
    ///     Full request body as JSON. Caller is responsible for adding
    ///     <c>messaging_product=whatsapp</c>; the client only adds auth and content-type.
    /// </param>
    Task<Result> UpdateBusinessProfileAsync(
        string phoneNumberId,
        string accessToken,
        string serializedPayload,
        CancellationToken cancellationToken
    );
}

/// <summary>
///     Account-SCS-local Meta WhatsApp Cloud API client. Deliberately separate from the
///     identically-named client in the Main SCS: the two SCSs may not reference each other and
///     the surface needed here (resumable upload + business profile) does not overlap with the
///     surface needed there (message sending). Graph version defaults to <c>v25.0</c> because the
///     Resumable Upload API requires v17.0+.
/// </summary>
public sealed class WhatsAppCloudApiClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WhatsAppCloudApiClient> logger
) : IWhatsAppCloudApiClient
{
    public const string HttpClientName = "account-whatsapp-cloud-api";

    private string BaseUrl => configuration["WhatsApp:GraphApiBaseUrl"] ?? "https://graph.facebook.com/v25.0/";

    public async Task<Result<string>> UploadProfilePictureAsync(
        string appId,
        string accessToken,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        // Step 1: create an upload session and obtain the upload id.
        var client = CreateClient(accessToken);
        var startUrl = $"{appId}/uploads?file_length={bytes.Length}&file_type={Uri.EscapeDataString(contentType)}";

        using var startResponse = await client.PostAsync(startUrl, content: null, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
        {
            var raw = await startResponse.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Resumable upload start failed: HTTP {Status} {Body}", (int)startResponse.StatusCode, raw);
            return Result<string>.BadRequest($"Failed to start resumable upload: HTTP {(int)startResponse.StatusCode}");
        }

        var startBody = await startResponse.Content.ReadFromJsonAsync<UploadSessionResponse>(cancellationToken: cancellationToken);
        if (startBody is null || string.IsNullOrWhiteSpace(startBody.Id))
        {
            return Result<string>.BadRequest("Resumable upload start returned no session id.");
        }

        // Step 2: POST the bytes to the session id. Auth header uses the "OAuth" scheme per Meta docs.
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, startBody.Id);
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        uploadRequest.Headers.TryAddWithoutValidation("file_offset", "0");
        uploadRequest.Content = new ByteArrayContent(bytes);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // The upload step uses a fresh HttpClient through the same factory so the base address
        // applies (the session id returned by step 1 is a relative resource, e.g. "upload:ABC123").
        var uploadClient = CreateClient(accessToken);
        using var uploadResponse = await uploadClient.SendAsync(uploadRequest, cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var raw = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Resumable upload bytes failed: HTTP {Status} {Body}", (int)uploadResponse.StatusCode, raw);
            return Result<string>.BadRequest($"Failed to upload bytes: HTTP {(int)uploadResponse.StatusCode}");
        }

        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<UploadHandleResponse>(cancellationToken: cancellationToken);
        if (uploadBody is null || string.IsNullOrWhiteSpace(uploadBody.Handle))
        {
            return Result<string>.BadRequest("Resumable upload returned no handle.");
        }

        return Result<string>.Success(uploadBody.Handle);
    }

    public async Task<Result> UpdateBusinessProfileAsync(
        string phoneNumberId,
        string accessToken,
        string serializedPayload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedPayload);

        var client = CreateClient(accessToken);
        var url = $"{phoneNumberId}/whatsapp_business_profile";

        using var content = new StringContent(serializedPayload, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("WhatsApp business profile update failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
        return Result.BadRequest($"Failed to update WhatsApp business profile: HTTP {(int)response.StatusCode}");
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

    private sealed record UploadSessionResponse(string? Id);

    private sealed record UploadHandleResponse([property: System.Text.Json.Serialization.JsonPropertyName("h")] string? Handle);
}
