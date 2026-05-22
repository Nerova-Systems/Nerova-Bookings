using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SharedKernel.Authentication.ApiKey;

/// <summary>
///     Authentication handler for Nerova API key tokens.
///     Accepts tokens via the <c>X-Api-Key</c> header or as a <c>Bearer nerova_*</c> value
///     in the <c>Authorization</c> header.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyValidator validator
) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;

        // X-Api-Key header takes precedence over the Authorization header.
        if (Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            token = apiKeyHeader.ToString();
        }
        else
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = authHeader["Bearer ".Length..].Trim();
                if (candidate.StartsWith(ApiKeyAuthenticationDefaults.TokenPrefix, StringComparison.Ordinal))
                {
                    token = candidate;
                }
            }
        }

        if (token is null) return AuthenticateResult.NoResult();

        var principal = await validator.ValidateAsync(token, Context.RequestAborted);
        if (principal is null) return AuthenticateResult.Fail("Invalid, expired, or revoked API key.");

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
