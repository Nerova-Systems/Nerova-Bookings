using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Wire format Meta sends to every WhatsApp Flow endpoint request.
/// </summary>
[PublicAPI]
public sealed record EncryptedFlowRequest(
    string EncryptedAesKey,
    string EncryptedFlowData,
    string InitialVector
);

/// <summary>
///     Outcome of dispatching an encrypted Meta request: the response body to return (already
///     encrypted) plus a flag telling the endpoint whether to surface a 4xx (e.g. unknown phone
///     number, decryption failure) instead of returning the encrypted payload to Meta.
/// </summary>
[PublicAPI]
public sealed record FlowDispatchOutcome(int StatusCode, string Body);

public interface IWhatsAppFlowDispatcher
{
    Task<FlowDispatchOutcome> Dispatch(EncryptedFlowRequest request, string? phoneNumberId, CancellationToken cancellationToken);
}

/// <summary>
///     Orchestrates the full Meta-encrypted-endpoint protocol:
///     <list type="number">
///         <item>Resolve the tenant by Meta phone-number id (404 if unknown).</item>
///         <item>Decrypt the AES key + body using the tenant's private key.</item>
///         <item>Short-circuit <c>ping</c>; otherwise route the screen to a handler.</item>
///         <item>Encrypt the response (AES key reused, IV bit-inverted) and emit base64 text.</item>
///     </list>
///     Any unhandled exception is converted to an encrypted error response so Meta still gets a
///     valid envelope.
/// </summary>
public sealed class WhatsAppFlowDispatcher(
    IWhatsAppFlowProfileSync profileSync,
    IWabaFlowDataCipher cipher,
    ITenantFlowConfigRepository tenantFlowConfigRepository,
    IEnumerable<IFlowScreenHandler> handlers,
    ILogger<WhatsAppFlowDispatcher> logger
) : IWhatsAppFlowDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<FlowDispatchOutcome> Dispatch(EncryptedFlowRequest request, string? phoneNumberId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return new FlowDispatchOutcome(400, "Missing X-WABA-Phone-Number-Id header.");
        }

        var profile = await profileSync.GetByPhoneNumberId(phoneNumberId, cancellationToken);
        if (profile is null || !profile.IsOnboardingComplete
            || string.IsNullOrWhiteSpace(profile.EncryptedPrivateKey)
            || string.IsNullOrWhiteSpace(profile.PrivateKeyIv))
        {
            return new FlowDispatchOutcome(404, "Unknown WABA phone number.");
        }

        var decryptResult = cipher.Decrypt(
            request.EncryptedAesKey,
            request.EncryptedFlowData,
            request.InitialVector,
            profile.EncryptedPrivateKey,
            profile.PrivateKeyIv
        );

        if (!decryptResult.IsSuccess || decryptResult.Value is null)
        {
            logger.LogWarning("Decryption failed for phone number {PhoneNumberId}", phoneNumberId);
            // Spec: when decryption fails Meta requires a 421 so it can rotate the key.
            return new FlowDispatchOutcome(421, "Decryption failed.");
        }

        var decrypted = decryptResult.Value;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(decrypted.PlaintextJson);
        }
        catch (JsonException)
        {
            return EncryptError(decrypted, "Malformed plaintext.");
        }

        using (document)
        {
            var root = document.RootElement;
            var action = root.TryGetProperty("action", out var actionValue) ? actionValue.GetString() ?? string.Empty : string.Empty;

            if (string.Equals(action, "ping", StringComparison.OrdinalIgnoreCase))
            {
                var pingBody = new JsonObject { ["data"] = new JsonObject { ["status"] = "active" } };
                return Encrypt(decrypted, pingBody);
            }

            var screen = root.TryGetProperty("screen", out var screenValue) ? screenValue.GetString() ?? string.Empty : string.Empty;
            var data = root.TryGetProperty("data", out var dataValue) ? dataValue.Clone() : default;
            var flowToken = root.TryGetProperty("flow_token", out var tokenValue) ? tokenValue.GetString() ?? string.Empty : string.Empty;

            var config = await tenantFlowConfigRepository.GetByTenantIdAsync(profile.TenantId, cancellationToken);
            if (config is null)
            {
                return EncryptError(decrypted, "Tenant flow configuration not found.");
            }

            var handler = handlers.FirstOrDefault(h => string.Equals(h.ScreenId, screen, StringComparison.OrdinalIgnoreCase));
            if (handler is null)
            {
                return EncryptError(decrypted, $"Unknown screen '{screen}'.");
            }

            try
            {
                var handlerRequest = new FlowScreenRequest(action, data, flowToken, profile.TenantId);
                var response = await handler.Handle(handlerRequest, config, cancellationToken);
                var body = BuildResponseBody(response);
                return Encrypt(decrypted, body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Flow handler '{Screen}' threw for tenant {TenantId}", screen, profile.TenantId);
                return EncryptError(decrypted, "Internal error");
            }
        }
    }

    private static JsonObject BuildResponseBody(FlowScreenResponse response)
    {
        var body = new JsonObject { ["data"] = response.Data };
        if (!string.IsNullOrWhiteSpace(response.NextScreen))
        {
            body["screen"] = response.NextScreen;
        }

        return body;
    }

    private FlowDispatchOutcome Encrypt(DecryptedFlowRequest decrypted, JsonObject body)
    {
        var json = body.ToJsonString(JsonOptions);
        var encrypted = cipher.Encrypt(json, decrypted.AesKey, decrypted.InitialVector);
        return new FlowDispatchOutcome(200, encrypted);
    }

    private FlowDispatchOutcome EncryptError(DecryptedFlowRequest decrypted, string message)
    {
        var body = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["acknowledged"] = true,
                ["error_message"] = message
            }
        };
        return Encrypt(decrypted, body);
    }
}
