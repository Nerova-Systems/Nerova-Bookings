using System.Text.Json;
using Main.Database;
using Main.Features.WhatsAppBooking.Domain;
using SharedKernel.Domain;
using SharedKernel.Integrations.Email;
using SharedKernel.Platform;

namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     Handles decrypted Meta Flows data-exchange requests for the WhatsApp Login/Registration Flow.
///     Screens: DETAILS (name + email; phone prefilled with the verified sender) -> OTP_VERIFY -> CONFIRM.
///     The OTP is emailed via the platform email client and the email doubles as an account-recovery key.
/// </summary>
public sealed class WhatsAppLoginFlowDataEndpoint(
    IWhatsAppConversationRepository conversationRepository,
    IWhatsAppLoginChallengeRepository challengeRepository,
    IEmailClient emailClient,
    WhatsAppFlowCrypto crypto,
    MainDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<WhatsAppLoginFlowDataEndpoint> logger
)
{
    private const string FlowVersion = "3.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public async Task<string> HandleEncryptedAsync(
        string encryptedAesKey,
        string encryptedFlowData,
        string initialVector,
        CancellationToken ct)
    {
        var decrypted = crypto.Decrypt(encryptedAesKey, encryptedFlowData, initialVector);
        var responseJson = await ProcessAsync(decrypted.Json, ct);
        return crypto.Encrypt(responseJson, decrypted.AesKey, decrypted.Iv);
    }

    private async Task<string> ProcessAsync(string requestJson, CancellationToken ct)
    {
        FlowDataRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<FlowDataRequest>(requestJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse Login Flow request");
            return Ping();
        }

        if (req is null || req.Action == "ping") return Ping();
        if (req.Action == "init")
        {
            // The Flow is normally launched directly on DETAILS with the phone prefilled; handle init
            // defensively by resolving the verified sender from the flow_token.
            var (_, initPhone) = await ResolveConversationAsync(req.FlowToken, ct);
            return DetailsScreen(initPhone ?? string.Empty, string.Empty, string.Empty);
        }

        return req.Screen switch
        {
            "DETAILS" => await HandleDetailsAsync(req, ct),
            "OTP_VERIFY" => await HandleOtpVerifyAsync(req, ct),
            "CONFIRM" => Ping(), // terminal screen completes client-side — just ack
            _ => Ping()
        };
    }

    // ── Handlers ──────────────────────────────────────────────────────────────────

    private async Task<string> HandleDetailsAsync(FlowDataRequest req, CancellationToken ct)
    {
        var name = req.DataString("name")?.Trim() ?? string.Empty;
        var email = req.DataString("email")?.Trim().ToLowerInvariant() ?? string.Empty;
        var phoneDisplay = req.DataString("phone")?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return DetailsScreen(phoneDisplay, name, email, "Please enter your name and a valid email address.");
        }

        var (tenantId, phone) = await ResolveConversationAsync(req.FlowToken, ct);
        if (tenantId is null || phone is null)
        {
            return DetailsScreen(phoneDisplay, name, email, "Session expired. Please start again.");
        }

        // Send the OTP to the entered email. The challenge is keyed by the verified WhatsApp sender,
        // so it is the verified phone — not a typed value — that ties the code to this conversation.
        var sent = await SendOtpAsync(tenantId, phone, email, ct);
        if (!sent)
        {
            return DetailsScreen(phone, name, email, "Failed to send verification code. Please try again.");
        }

        return OtpVerifyScreen(name, email, phone);
    }

    private async Task<string> HandleOtpVerifyAsync(FlowDataRequest req, CancellationToken ct)
    {
        var otp = req.DataString("otp")?.Trim();
        var name = req.DataString("name") ?? string.Empty;
        var email = req.DataString("email") ?? string.Empty;
        var phoneDisplay = req.DataString("phone") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(otp))
        {
            return OtpVerifyScreen(name, email, phoneDisplay, "Please enter the verification code.");
        }

        var (tenantId, phone) = await ResolveConversationAsync(req.FlowToken, ct);
        if (tenantId is null || phone is null)
        {
            return OtpVerifyScreen(name, email, phoneDisplay, "Session expired. Please start again.");
        }

        var challenge = await challengeRepository.GetByTenantAndPhoneUnfilteredAsync(tenantId, phone, ct);
        if (challenge is null)
        {
            return OtpVerifyScreen(name, email, phone, "Session expired. Please start again.");
        }

        if (!challenge.Validate(otp, timeProvider.GetUtcNow()))
        {
            return OtpVerifyScreen(name, email, phone, "Invalid or expired code. Please try again.");
        }

        challenge.Consume();
        challengeRepository.Update(challenge);
        await dbContext.SaveChangesAsync(ct);

        // Navigate to the terminal CONFIRM screen with the verified phone; the user taps Confirm there,
        // which completes the Flow (complete action) and emits the nfm_reply the engine consumes.
        return ConfirmScreen(name, email, phone);
    }

    // ── OTP helper ────────────────────────────────────────────────────────────────

    private async Task<bool> SendOtpAsync(TenantId tenantId, string phone, string email, CancellationToken ct)
    {
        // Remove any existing challenge.
        var existing = await challengeRepository.GetByTenantAndPhoneUnfilteredAsync(tenantId, phone, ct);
        if (existing is not null)
        {
            challengeRepository.Remove(existing);
            await dbContext.SaveChangesAsync(ct);
        }

        var (challenge, otp) = WhatsAppLoginChallenge.Create(tenantId, phone, email, timeProvider.GetUtcNow());
        await challengeRepository.AddAsync(challenge, ct);
        await dbContext.SaveChangesAsync(ct);

        try
        {
            var productName = Settings.Current.Branding.ProductName;
            await emailClient.SendAsync(new EmailMessage(
                    email,
                    $"{productName} verification code",
                    $"<p>Your {productName} verification code is: <strong>{otp}</strong></p><p>It expires in 15 minutes.</p>",
                    $"Your {productName} verification code is: {otp}. It expires in 15 minutes."
                ), ct
            );
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OTP email to {Email}", email);
            return false;
        }
    }

    // ── Screen builders ────────────────────────────────────────────────────────────

    private static string Ping()
    {
        return JsonSerializer.Serialize(new { version = FlowVersion, data = new { status = "active" } }, JsonOptions);
    }

    private static string DetailsScreen(string phone, string name, string email, string errorMessage = "")
    {
        return JsonSerializer.Serialize(new { version = FlowVersion, screen = "DETAILS", data = new { name, email, phone, error_message = errorMessage } }, JsonOptions);
    }

    private static string OtpVerifyScreen(string name, string email, string phone, string errorMessage = "")
    {
        return JsonSerializer.Serialize(new { version = FlowVersion, screen = "OTP_VERIFY", data = new { name, email, phone, error_message = errorMessage } }, JsonOptions);
    }

    private static string ConfirmScreen(string name, string email, string phone)
    {
        return JsonSerializer.Serialize(new { version = FlowVersion, screen = "CONFIRM", data = new { name, email, phone } }, JsonOptions);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<(TenantId? TenantId, string? Phone)> ResolveConversationAsync(string? flowToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(flowToken)) return (null, null);
        const string prefix = "login-";
        if (!flowToken.StartsWith(prefix, StringComparison.Ordinal)) return (null, null);
        var rawId = flowToken[prefix.Length..];
        WhatsAppConversationId conversationId;
        try
        {
            conversationId = new WhatsAppConversationId(rawId);
        }
        catch
        {
            return (null, null);
        }

        var conversation = await conversationRepository.GetByIdAsync(conversationId, ct);
        return conversation is null ? (null, null) : (conversation.TenantId, conversation.CustomerPhoneNumber);
    }
}

internal sealed class FlowDataRequest
{
    public string? Version { get; init; }

    public string? Action { get; init; }

    public string? Screen { get; init; }

    public JsonElement? Data { get; init; }

    [JsonPropertyName("flow_token")]
    public string? FlowToken { get; init; }

    public string? DataString(string key)
    {
        if (!Data.HasValue) return null;
        return Data.Value.TryGetProperty(key, out var p) ? p.GetString() : null;
    }
}

internal static class JsonElementExtensions
{
    public static string? TryGetString(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var p) ? p.GetString() : null;
    }
}
