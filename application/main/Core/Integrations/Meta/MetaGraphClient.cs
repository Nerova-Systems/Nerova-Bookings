using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace Main.Integrations.Meta;

/// <summary>
///     Typed HttpClient for the Meta Graph API (https://graph.facebook.com/). Used to complete WhatsApp
///     Embedded Signup onboarding. Following the project's integration rules this client never throws:
///     every failure path logs and returns null/false so command handlers can map it to a clean Result.
/// </summary>
public sealed class MetaGraphClient(HttpClient httpClient, IConfiguration configuration, ILogger<MetaGraphClient> logger) : IMetaGraphClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly string _appId = configuration["Meta:AppId"] ?? "not-configured";
    private readonly string _appSecret = configuration["Meta:AppSecret"] ?? "not-configured";
    private readonly string _graphApiVersion = configuration["Meta:GraphApiVersion"] ?? "v21.0";

    public async Task<string?> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _appId,
                    ["client_secret"] = _appSecret,
                    ["code"] = code
                }
            );
            var response = await httpClient.PostAsync($"{_graphApiVersion}/oauth/access_token", formContent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to exchange Meta authorization code. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<MetaTokenResponse>(JsonSerializerOptions, cancellationToken);
            if (string.IsNullOrEmpty(token?.AccessToken))
            {
                logger.LogError("Meta authorization code exchange returned an empty access token");
                return null;
            }

            return token.AccessToken;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error exchanging Meta authorization code for an access token");
            return null;
        }
    }

    public async Task<bool> RegisterPhoneNumberAsync(string phoneNumberId, string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var pin = Random.Shared.Next(0, 1_000_000).ToString("D6");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphApiVersion}/{phoneNumberId}/register");
            request.Content = JsonContent.Create(new MetaRegisterPhoneNumberRequest("whatsapp", pin), options: JsonSerializerOptions);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to register WhatsApp phone number '{PhoneNumberId}'. Status code: {StatusCode}", phoneNumberId, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<MetaSuccessResponse>(JsonSerializerOptions, cancellationToken);
            return result?.Success == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error registering WhatsApp phone number '{PhoneNumberId}'", phoneNumberId);
            return false;
        }
    }

    public async Task<bool> SubscribeAppToWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphApiVersion}/{wabaId}/subscribed_apps");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to subscribe app to WhatsApp Business Account '{WabaId}'. Status code: {StatusCode}", wabaId, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<MetaSuccessResponse>(JsonSerializerOptions, cancellationToken);
            return result?.Success == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error subscribing app to WhatsApp Business Account '{WabaId}'", wabaId);
            return false;
        }
    }

    public async Task<MetaWabaMetadata?> GetWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_graphApiVersion}/{wabaId}?fields=id,name");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch WhatsApp Business Account '{WabaId}'. Status code: {StatusCode}", wabaId, response.StatusCode);
                return null;
            }

            var waba = await response.Content.ReadFromJsonAsync<MetaWabaResponse>(JsonSerializerOptions, cancellationToken);
            if (waba is null || string.IsNullOrEmpty(waba.Id))
            {
                logger.LogError("WhatsApp Business Account '{WabaId}' response was empty", wabaId);
                return null;
            }

            return new MetaWabaMetadata(waba.Id, waba.Name ?? string.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error fetching WhatsApp Business Account '{WabaId}'", wabaId);
            return null;
        }
    }

    public async Task<MetaPhoneNumber[]?> GetPhoneNumbersAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_graphApiVersion}/{wabaId}/phone_numbers?fields=id,display_phone_number,verified_name");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch phone numbers for WhatsApp Business Account '{WabaId}'. Status code: {StatusCode}", wabaId, response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MetaPhoneNumbersResponse>(JsonSerializerOptions, cancellationToken);
            if (result?.Data is null)
            {
                logger.LogError("Phone numbers response for WhatsApp Business Account '{WabaId}' was empty", wabaId);
                return null;
            }

            return [.. result.Data.Select(p => new MetaPhoneNumber(p.Id, p.DisplayPhoneNumber ?? string.Empty, p.VerifiedName ?? string.Empty))];
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error fetching phone numbers for WhatsApp Business Account '{WabaId}'", wabaId);
            return null;
        }
    }

    public async Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string toPhoneNumber, string text, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphApiVersion}/{phoneNumberId}/messages");
            request.Content = JsonContent.Create(
                new MetaSendMessageRequest("whatsapp", toPhoneNumber, "text", new MetaTextContent(text)),
                options: JsonSerializerOptions
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to send WhatsApp message to '{ToPhoneNumber}'. Status code: {StatusCode}", toPhoneNumber, response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MetaSendMessageResponse>(JsonSerializerOptions, cancellationToken);
            var messageId = result?.Messages?.FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(messageId))
            {
                logger.LogError("WhatsApp send message response contained no message ID");
                return null;
            }

            return messageId;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error sending WhatsApp message to '{ToPhoneNumber}'", toPhoneNumber);
            return null;
        }
    }

    public async Task<string?> SendInteractiveButtonsAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        IReadOnlyList<WhatsAppReplyButton> buttons,
        CancellationToken cancellationToken
    )
    {
        var payload = new
        {
            MessagingProduct = "whatsapp",
            To = toPhoneNumber,
            Type = "interactive",
            Interactive = new
            {
                Type = "button",
                Body = new { Text = bodyText },
                Action = new
                {
                    Buttons = buttons
                        .Select(button => new { Type = "reply", Reply = new { button.Id, button.Title } })
                        .ToArray()
                }
            }
        };

        return await PostInteractiveMessageAsync(phoneNumberId, accessToken, toPhoneNumber, payload, cancellationToken);
    }

    public async Task<string?> SendInteractiveListAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string buttonLabel,
        IReadOnlyList<WhatsAppListSection> sections,
        CancellationToken cancellationToken
    )
    {
        var payload = new
        {
            MessagingProduct = "whatsapp",
            To = toPhoneNumber,
            Type = "interactive",
            Interactive = new
            {
                Type = "list",
                Body = new { Text = bodyText },
                Action = new
                {
                    Button = buttonLabel,
                    Sections = sections
                        .Select(section => new
                        {
                            section.Title,
                            Rows = section.Rows
                                .Select(row => row.Description is null
                                    ? (object)new { row.Id, row.Title }
                                    : new { row.Id, row.Title, row.Description })
                                .ToArray()
                        })
                        .ToArray()
                }
            }
        };

        return await PostInteractiveMessageAsync(phoneNumberId, accessToken, toPhoneNumber, payload, cancellationToken);
    }

    public async Task<string?> SendCtaUrlButtonAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string buttonText,
        string url,
        CancellationToken cancellationToken
    )
    {
        var payload = new
        {
            MessagingProduct = "whatsapp",
            To = toPhoneNumber,
            Type = "interactive",
            Interactive = new
            {
                Type = "cta_url",
                Body = new { Text = bodyText },
                Action = new
                {
                    Name = "cta_url",
                    Parameters = new { DisplayText = buttonText, Url = url }
                }
            }
        };

        return await PostInteractiveMessageAsync(phoneNumberId, accessToken, toPhoneNumber, payload, cancellationToken);
    }

    public async Task<string?> SendFlowMessageAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string flowId,
        string flowToken,
        string flowCtaText,
        string? initialScreen,
        object? initialData,
        CancellationToken cancellationToken
    )
    {
        object parameters = initialScreen is null
            ? new
            {
                FlowMessageVersion = "3",
                FlowToken = flowToken,
                FlowId = flowId,
                FlowCta = flowCtaText,
                FlowAction = "data_exchange"
            }
            : new
            {
                FlowMessageVersion = "3",
                FlowToken = flowToken,
                FlowId = flowId,
                FlowCta = flowCtaText,
                FlowAction = "navigate",
                FlowActionPayload = initialData is null
                    ? (object)new { Screen = initialScreen }
                    : new { Screen = initialScreen, Data = initialData }
            };

        var payload = new
        {
            MessagingProduct = "whatsapp",
            To = toPhoneNumber,
            Type = "interactive",
            Interactive = new
            {
                Type = "flow",
                Body = new { Text = bodyText },
                Action = new
                {
                    Name = "flow",
                    Parameters = parameters
                }
            }
        };

        return await PostInteractiveMessageAsync(phoneNumberId, accessToken, toPhoneNumber, payload, cancellationToken);
    }

    /// <summary>
    ///     Shared POST + response parsing for interactive (button/list/cta_url/flow) messages. On a non-success
    ///     status the Meta error body is logged to aid debugging. Never throws — returns null on any failure.
    /// </summary>
    private async Task<string?> PostInteractiveMessageAsync(string phoneNumberId, string accessToken, string toPhoneNumber, object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_graphApiVersion}/{phoneNumberId}/messages");
            request.Content = JsonContent.Create(payload, options: JsonSerializerOptions);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Failed to send WhatsApp interactive message to '{ToPhoneNumber}'. Status code: {StatusCode}. Body: {ErrorBody}",
                    toPhoneNumber,
                    response.StatusCode,
                    errorBody
                );
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MetaSendMessageResponse>(JsonSerializerOptions, cancellationToken);
            var messageId = result?.Messages?.FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(messageId))
            {
                logger.LogError("WhatsApp interactive send response to '{ToPhoneNumber}' contained no message ID", toPhoneNumber);
                return null;
            }

            return messageId;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Error sending WhatsApp interactive message to '{ToPhoneNumber}'", toPhoneNumber);
            return null;
        }
    }

    private sealed record MetaTokenResponse(string? AccessToken);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaRegisterPhoneNumberRequest(string MessagingProduct, string Pin);

    private sealed record MetaSuccessResponse(bool Success);

    private sealed record MetaWabaResponse(string Id, string? Name);

    private sealed record MetaPhoneNumbersResponse(MetaPhoneNumberData[]? Data);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaPhoneNumberData(string Id, string? DisplayPhoneNumber, string? VerifiedName);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaSendMessageRequest(string MessagingProduct, string To, string Type, MetaTextContent Text);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaTextContent(string Body);

    private sealed record MetaSendMessageResponse(MetaSentMessageId[]? Messages);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaSentMessageId(string? Id);
}
