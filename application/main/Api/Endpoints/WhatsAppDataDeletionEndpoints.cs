using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

/// <summary>
///     Implements the Facebook/Meta Data Deletion Callback required for apps using Facebook Login
///     or WhatsApp Embedded Signup. Meta sends a POST with a signed_request when a user requests
///     data deletion from Facebook's privacy controls. The app verifies the signature and returns
///     a confirmation code and status URL.
///     See: https://developers.facebook.com/docs/apps/delete-data
/// </summary>
public sealed class WhatsAppDataDeletionEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/whatsapp/data-deletion";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppDataDeletion");

        // Data Deletion Callback: Meta sends signed_request as application/x-www-form-urlencoded.
        // Inline logic required because the request body must be read as form data and the
        // response is plain JSON — not an ApiResult wrapper.
        group.MapPost("/", async (HttpRequest request, IConfiguration configuration) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest();
                }

                var form = await request.ReadFormAsync();
                var signedRequest = form["signed_request"].ToString();
                if (string.IsNullOrWhiteSpace(signedRequest))
                {
                    return Results.BadRequest();
                }

                var parts = signedRequest.Split('.', 2);
                if (parts.Length != 2)
                {
                    return Results.BadRequest();
                }

                var encodedSignature = parts[0];
                var encodedPayload = parts[1];

                var appSecret = configuration["Meta:AppSecret"] ?? string.Empty;
                if (string.IsNullOrEmpty(appSecret))
                {
                    return Results.Problem("Meta:AppSecret is not configured.", statusCode: StatusCodes.Status500InternalServerError);
                }

                // Verify: HMAC-SHA256(base64url-decoded payload, app_secret) must match decoded signature
                var keyBytes = Encoding.UTF8.GetBytes(appSecret);
                var payloadBytes = Encoding.UTF8.GetBytes(encodedPayload);
                var expectedHash = HMACSHA256.HashData(keyBytes, payloadBytes);
                var expectedSignature = Base64UrlEncodeBytes(expectedHash);

                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(expectedSignature),
                        Encoding.ASCII.GetBytes(encodedSignature)))
                {
                    return Results.Unauthorized();
                }

                // Decode the payload to extract the Facebook user_id
                string? userId = null;
                try
                {
                    var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(encodedPayload.Replace('-', '+').Replace('_', '/'))));
                    using var doc = JsonDocument.Parse(payloadJson);
                    if (doc.RootElement.TryGetProperty("user_id", out var userIdElement))
                    {
                        userId = userIdElement.GetString();
                    }
                }
                catch (Exception)
                {
                    return Results.BadRequest();
                }

                // Generate a deterministic confirmation code from the user_id so it can be looked up later.
                // No personal data stored — we do not retain Facebook user IDs in our database.
                var confirmationCode = ComputeConfirmationCode(userId ?? string.Empty, appSecret);
                var appUrl = configuration["Meta:AppUrl"] ?? "https://app.nerovasystems.com";
                var statusUrl = $"{appUrl.TrimEnd('/')}/data-deletion?id={confirmationCode}";

                return Results.Json(new { url = statusUrl, confirmation_code = confirmationCode });
            }
        ).AllowAnonymous().DisableAntiforgery();
    }

    private static string ComputeConfirmationCode(string userId, string appSecret)
    {
        var input = Encoding.UTF8.GetBytes($"{userId}:{appSecret}");
        var hash = SHA256.HashData(input);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string Base64UrlEncodeBytes(byte[] data)
    {
        return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string PadBase64(string base64)
    {
        return (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64
        };
    }
}
