using System.Text.Json;
using Main.Database;
using Main.Features.Clients.Domain;
using Main.Features.WhatsAppBooking.Domain;
using SharedKernel.Domain;
using SharedKernel.Integrations.Email;

namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     Handles decrypted Meta Flows data-exchange requests for the WhatsApp Login/Registration Flow.
///     Screens: SIGN_IN (email lookup) -> OTP_VERIFY or SIGN_UP -> OTP_VERIFY -> SUCCESS.
/// </summary>
public sealed class WhatsAppLoginFlowDataEndpoint(
    IWhatsAppConversationRepository conversationRepository,
    IWhatsAppLoginChallengeRepository challengeRepository,
    IClientRepository clientRepository,
    IEmailClient emailClient,
    WhatsAppFlowCrypto crypto,
    MainDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<WhatsAppLoginFlowDataEndpoint> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private const string FlowVersion = "3.0";

    public async Task<string> HandleEncryptedAsync(
        string encryptedAesKey, string encryptedFlowData, string initialVector,
        CancellationToken ct)
    {
        var decrypted = crypto.Decrypt(encryptedAesKey, encryptedFlowData, initialVector);
        var responseJson = await ProcessAsync(decrypted.Json, ct);
        return crypto.Encrypt(responseJson, decrypted.AesKey, decrypted.Iv);
    }

    private async Task<string> ProcessAsync(string requestJson, CancellationToken ct)
    {
        FlowDataRequest? req;
        try { req = JsonSerializer.Deserialize<FlowDataRequest>(requestJson, JsonOptions); }
        catch (JsonException ex) { logger.LogWarning(ex, "Failed to parse Login Flow request"); return Ping(); }

        if (req is null || req.Action == "ping") return Ping();
        if (req.Action == "init") return SignInScreen(string.Empty);

        return req.Screen switch
        {
            "SIGN_IN" => await HandleSignInAsync(req, ct),
            "SIGN_UP" => await HandleSignUpAsync(req, ct),
            "OTP_VERIFY" => await HandleOtpVerifyAsync(req, ct),
            "SUCCESS" => Ping(), // terminal screen submission — just ack
            _ => Ping()
        };
    }

    // ── Handlers ──────────────────────────────────────────────────────────────────

    private async Task<string> HandleSignInAsync(FlowDataRequest req, CancellationToken ct)
    {
        var email = req.DataString("email");
        if (string.IsNullOrWhiteSpace(email)) return SignInScreen("Please enter a valid email address.");

        var (tenantId, phone) = await ResolveConversationAsync(req.FlowToken, ct);
        if (tenantId is null || phone is null) return SignInScreen("Session expired. Please start again.");

        // Check if this email matches an existing client for this tenant.
        var client = await clientRepository.GetByTenantAndContactUnfilteredAsync(tenantId, null, email.Trim().ToLowerInvariant(), ct);
        var name = client is not null ? $"{client.FirstName} {client.LastName}".Trim() : string.Empty;

        // Send OTP and navigate to OTP_VERIFY.
        var sent = await SendOtpAsync(tenantId, phone, email.Trim().ToLowerInvariant(), ct);
        if (!sent) return SignInScreen("Failed to send verification code. Please try again.");

        return OtpVerifyScreen(email.Trim().ToLowerInvariant(), name);
    }

    private async Task<string> HandleSignUpAsync(FlowDataRequest req, CancellationToken ct)
    {
        var firstName = req.DataString("first_name")?.Trim();
        var lastName = req.DataString("last_name")?.Trim();
        var email = req.DataString("email")?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(email))
            return SignUpScreen(email ?? string.Empty);

        var (tenantId, phone) = await ResolveConversationAsync(req.FlowToken, ct);
        if (tenantId is null || phone is null) return SignUpScreen(email);

        var sent = await SendOtpAsync(tenantId, phone, email, ct);
        if (!sent) return SignUpScreen(email);

        var name = $"{firstName} {lastName}".Trim();
        return OtpVerifyScreen(email, name);
    }

    private async Task<string> HandleOtpVerifyAsync(FlowDataRequest req, CancellationToken ct)
    {
        var otp = req.DataString("otp")?.Trim();
        var name = req.DataString("name") ?? string.Empty;
        var email = req.DataString("email") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(otp))
            return OtpVerifyScreen(email, name, "Please enter the verification code.");

        var (tenantId, phone) = await ResolveConversationAsync(req.FlowToken, ct);
        if (tenantId is null || phone is null)
            return OtpVerifyScreen(email, name, "Session expired. Please start again.");

        var challenge = await challengeRepository.GetByTenantAndPhoneUnfilteredAsync(tenantId, phone, ct);
        if (challenge is null)
            return OtpVerifyScreen(email, name, "Session expired. Please start again.");

        if (!challenge.Validate(otp, timeProvider.GetUtcNow()))
            return OtpVerifyScreen(email, name, "Invalid or expired code. Please try again.");

        challenge.Consume();
        challengeRepository.Update(challenge);
        await dbContext.SaveChangesAsync(ct);

        return SuccessScreen(name, email);
    }

    // ── OTP helper ────────────────────────────────────────────────────────────────

    private async Task<bool> SendOtpAsync(TenantId tenantId, string phone, string email, CancellationToken ct)
    {
        // Remove any existing challenge.
        var existing = await challengeRepository.GetByTenantAndPhoneUnfilteredAsync(tenantId, phone, ct);
        if (existing is not null) { challengeRepository.Remove(existing); await dbContext.SaveChangesAsync(ct); }

        var (challenge, otp) = WhatsAppLoginChallenge.Create(tenantId, phone, email, timeProvider.GetUtcNow());
        await challengeRepository.AddAsync(challenge, ct);
        await dbContext.SaveChangesAsync(ct);

        try
        {
            await emailClient.SendAsync(new SharedKernel.Integrations.Email.EmailMessage(
                email,
                "Your verification code",
                $"<p>Your verification code is: <strong>{otp}</strong></p><p>It expires in 15 minutes.</p>",
                $"Your verification code is: {otp}. It expires in 15 minutes."
            ), ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OTP email to {Email}", email);
            return false;
        }
    }

    // ── Screen builders ────────────────────────────────────────────────────────────

    private static string Ping() =>
        JsonSerializer.Serialize(new { version = FlowVersion, data = new { status = "active" } }, JsonOptions);

    private static string SignInScreen(string errorMessage) =>
        JsonSerializer.Serialize(new { version = FlowVersion, screen = "SIGN_IN", data = new { error_message = errorMessage } }, JsonOptions);

    private static string SignUpScreen(string email) =>
        JsonSerializer.Serialize(new { version = FlowVersion, screen = "SIGN_UP", data = new { email } }, JsonOptions);

    private static string OtpVerifyScreen(string email, string name, string errorMessage = "") =>
        JsonSerializer.Serialize(new { version = FlowVersion, screen = "OTP_VERIFY", data = new { email, name, error_message = errorMessage } }, JsonOptions);

    private static string SuccessScreen(string name, string email) =>
        JsonSerializer.Serialize(new { version = FlowVersion, screen = "SUCCESS", data = new { name, email } }, JsonOptions);

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<(TenantId? TenantId, string? Phone)> ResolveConversationAsync(string? flowToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(flowToken)) return (null, null);
        const string prefix = "login-";
        if (!flowToken.StartsWith(prefix, StringComparison.Ordinal)) return (null, null);
        var rawId = flowToken[prefix.Length..];
        WhatsAppConversationId conversationId;
        try { conversationId = new WhatsAppConversationId(rawId); } catch { return (null, null); }
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
        => element.TryGetProperty(propertyName, out var p) ? p.GetString() : null;
}
