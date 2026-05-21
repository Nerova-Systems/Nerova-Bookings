using Account.Features.SsoMicrosoft.Infrastructure;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.Cqrs;
using SharedKernel.OpenIdConnect;
using SharedKernel.Telemetry;

namespace Account.Features.SsoMicrosoft.Commands;

/// <summary>
///     Initiates the Microsoft SSO OIDC flow for a given organization slug.
///     Returns a redirect URL to the Microsoft authorization endpoint.
///     No authentication is required — this is the entry point for pre-login SSO.
/// </summary>
[PublicAPI]
public sealed record StartMicrosoftSsoCommand(string OrgSlug) : ICommand, IRequest<Result<string>>;

public sealed class StartMicrosoftSsoHandler(
    MicrosoftSsoConfigurator configurator,
    SsoStateService ssoStateService,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events
) : IRequestHandler<StartMicrosoftSsoCommand, Result<string>>
{
    public async Task<Result<string>> Handle(StartMicrosoftSsoCommand command, CancellationToken cancellationToken)
    {
        var resolved = await configurator.ResolveForOrgSlugAsync(command.OrgSlug, cancellationToken);
        if (resolved is null)
        {
            return Result<string>.NotFound("Microsoft SSO is not configured or not enabled for this organization.");
        }

        var codeVerifier = PkceUtilities.GenerateCodeVerifier();
        var codeChallenge = PkceUtilities.GenerateCodeChallenge(codeVerifier);
        var nonce = NonceUtilities.GenerateNonce();
        var fingerprintHash = ssoStateService.GenerateBrowserFingerprintHash();

        ssoStateService.SetStateCookie(resolved.OrgId, fingerprintHash, codeVerifier, nonce);

        var stateToken = ssoStateService.ProtectStateToken(resolved.OrgId);
        var redirectUri = SsoStateService.GetCallbackUrl();

        var authorizationUrl = BuildAuthorizationUrl(
            resolved.AzureTenantId,
            resolved.ClientId,
            redirectUri,
            stateToken,
            codeChallenge,
            nonce
        );

        events.CollectEvent(new MicrosoftSsoLoginStarted(resolved.OrgId));

        var httpContext = httpContextAccessor.HttpContext!;
        var returnPath = httpContext.Request.Query["ReturnPath"].ToString();
        if (!string.IsNullOrEmpty(returnPath))
        {
            ReturnPathHelper.SetReturnPathCookie(httpContext, returnPath);
        }

        return Result<string>.Redirect(authorizationUrl);
    }

    private static string BuildAuthorizationUrl(
        string azureTenantId,
        string clientId,
        string redirectUri,
        string stateToken,
        string codeChallenge,
        string nonce)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = stateToken,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["nonce"] = nonce,
            ["prompt"] = "select_account"
        };

        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{MicrosoftSsoConfigurator.GetAuthorizationEndpoint(azureTenantId)}?{queryString}";
    }
}
