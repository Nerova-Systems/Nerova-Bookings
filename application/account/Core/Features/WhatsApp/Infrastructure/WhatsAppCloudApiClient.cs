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

    /// <summary>
    ///     GETs the current <c>whatsapp_business_profile</c> from Meta. Used by the Phase 7b drift
    ///     detector to compare Meta's view against the tenant's local <c>BrandProfile</c>. The
    ///     wire response is shaped as <c>{ "data": [ { ... } ] }</c>; this method unwraps the
    ///     envelope and returns the single profile object (or a failed <see cref="Result" /> when
    ///     the envelope is empty/malformed).
    /// </summary>
    Task<Result<RemoteWabaProfileDto>> GetBusinessProfileAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     POSTs a new display-name change request to
    ///     <c>POST /{phone-number-id}/display_name</c>. Meta reviews the change for 1–3 business
    ///     days; the result is read back via <see cref="GetDisplayNameStatusAsync" />.
    /// </summary>
    Task<Result> RequestDisplayNameChangeAsync(
        string phoneNumberId,
        string accessToken,
        string requestedName,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     GETs <c>name_status</c> + <c>verified_name</c> for a phone number. Used by the Phase
    ///     7c poller to advance a pending-review aggregate to its terminal state.
    /// </summary>
    Task<Result<RemoteWabaDisplayNameStatusDto>> GetDisplayNameStatusAsync(
        string phoneNumberId,
        string accessToken,
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

    public async Task<Result<RemoteWabaProfileDto>> GetBusinessProfileAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var client = CreateClient(accessToken);
        // Explicit field projection — the default response omits most fields.
        var url = $"{phoneNumberId}/whatsapp_business_profile?fields=about,address,description,email,profile_picture_url,vertical,websites";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("WhatsApp business profile fetch failed: HTTP {Status} {Body}", (int)response.StatusCode, raw);
            return Result<RemoteWabaProfileDto>.BadRequest($"Failed to fetch WhatsApp business profile: HTTP {(int)response.StatusCode}");
        }

        var envelope = await response.Content.ReadFromJsonAsync<RemoteWabaProfileEnvelope>(cancellationToken: cancellationToken);
        var first = envelope?.Data?.FirstOrDefault();
        if (first is null)
        {
            return Result<RemoteWabaProfileDto>.BadRequest("WhatsApp business profile fetch returned no data.");
        }

        return Result<RemoteWabaProfileDto>.Success(first);
    }

    public async Task<Result> RequestDisplayNameChangeAsync(
        string phoneNumberId,
        string accessToken,
        string requestedName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedName);

        var client = CreateClient(accessToken);
        var url = $"{phoneNumberId}/display_name";

        // The Graph endpoint accepts the requested name as form-encoded data on the body. We send
        // the same field name Meta documents ("display_name") rather than masquerading as JSON so
        // future Graph versions that tighten content-type validation continue to work.
        using var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("display_name", requestedName)
            }
        );
        using var response = await client.PostAsync(url, content, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "WhatsApp display-name change request failed: HTTP {Status} {Body}",
            (int)response.StatusCode, raw
        );
        return Result.BadRequest($"Failed to request display-name change: HTTP {(int)response.StatusCode}");
    }

    public async Task<Result<RemoteWabaDisplayNameStatusDto>> GetDisplayNameStatusAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var client = CreateClient(accessToken);
        var url = $"{phoneNumberId}?fields=name_status,verified_name";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "WhatsApp display-name status fetch failed: HTTP {Status} {Body}",
                (int)response.StatusCode, raw
            );
            return Result<RemoteWabaDisplayNameStatusDto>.BadRequest(
                $"Failed to fetch display-name status: HTTP {(int)response.StatusCode}"
            );
        }

        var body = await response.Content.ReadFromJsonAsync<RemoteWabaDisplayNameStatusDto>(cancellationToken: cancellationToken);
        if (body is null)
        {
            return Result<RemoteWabaDisplayNameStatusDto>.BadRequest("Display-name status fetch returned no body.");
        }

        return Result<RemoteWabaDisplayNameStatusDto>.Success(body);
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

    private sealed record RemoteWabaProfileEnvelope(
        [property: System.Text.Json.Serialization.JsonPropertyName("data")]
        IReadOnlyList<RemoteWabaProfileDto>? Data
    );
}

/// <summary>
///     Wire shape of the Meta <c>GET /{phone_number_id}/whatsapp_business_profile</c> response
///     payload (the inner object inside the <c>data</c> array). Note the GET surface uses
///     <c>profile_picture_url</c> rather than <c>profile_picture_handle</c>: Meta exposes the
///     hosted CDN URL on read but only accepts an opaque resumable-upload handle on write.
/// </summary>
[PublicAPI]
public sealed record RemoteWabaProfileDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("about")]
    string? About,
    [property: System.Text.Json.Serialization.JsonPropertyName("address")]
    string? Address,
    [property: System.Text.Json.Serialization.JsonPropertyName("description")]
    string? Description,
    [property: System.Text.Json.Serialization.JsonPropertyName("email")]
    string? Email,
    [property: System.Text.Json.Serialization.JsonPropertyName("vertical")]
    string? Vertical,
    [property: System.Text.Json.Serialization.JsonPropertyName("websites")]
    IReadOnlyList<string>? Websites,
    [property: System.Text.Json.Serialization.JsonPropertyName("profile_picture_url")]
    string? ProfilePictureUrl
);

/// <summary>
///     Wire shape of <c>GET /{phone-number-id}?fields=name_status,verified_name</c>. The Graph
///     response is a flat object (no <c>data</c> envelope), and <c>name_status</c> is one of the
///     <see cref="Account.Features.WhatsApp.Domain.MetaNameStatus" /> codes — parsed by the
///     System.Text.Json string-enum converter applied to the enum.
/// </summary>
[PublicAPI]
public sealed record RemoteWabaDisplayNameStatusDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("name_status")]
    Account.Features.WhatsApp.Domain.MetaNameStatus NameStatus,
    [property: System.Text.Json.Serialization.JsonPropertyName("verified_name")]
    string? VerifiedName
);
