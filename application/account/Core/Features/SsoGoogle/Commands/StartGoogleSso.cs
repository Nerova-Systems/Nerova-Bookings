using Account.Features.SsoGoogle.Infrastructure;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.Cqrs;
using SharedKernel.OpenIdConnect;
using SharedKernel.Telemetry;

namespace Account.Features.SsoGoogle.Commands;

/// <summary>
///     Initiates the Google Workspace SSO OIDC flow for a given organization slug.
///     Returns a redirect URL to the Google authorization endpoint.
///     No authentication is required — this is the entry point for pre-login SSO.
/// </summary>
[PublicAPI]
public sealed record StartGoogleSsoCommand(string OrgSlug) : ICommand, IRequest<Result<string>>;

public sealed class StartGoogleSsoHandler(
    GoogleSsoConfigurator configurator,
    GoogleSsoStateService ssoStateService,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events
) : IRequestHandler<StartGoogleSsoCommand, Result<string>>
{
    public async Task<Result<string>> Handle(StartGoogleSsoCommand command, CancellationToken cancellationToken)
    {
        var resolved = await configurator.ResolveForOrgSlugAsync(command.OrgSlug, cancellationToken);
        if (resolved is null)
        {
            return Result<string>.NotFound("Google SSO is not configured or not enabled for this organization.");
        }

        var codeVerifier = PkceUtilities.GenerateCodeVerifier();
        var codeChallenge = PkceUtilities.GenerateCodeChallenge(codeVerifier);
        var nonce = NonceUtilities.GenerateNonce();
        var fingerprintHash = ssoStateService.GenerateBrowserFingerprintHash();

        ssoStateService.SetStateCookie(resolved.OrgId, fingerprintHash, codeVerifier, nonce);

        var stateToken = ssoStateService.ProtectStateToken(resolved.OrgId);
        var redirectUri = GoogleSsoStateService.GetCallbackUrl();

        var authorizationUrl = BuildAuthorizationUrl(
            resolved.ClientId,
            resolved.HostedDomain,
            redirectUri,
            stateToken,
            codeChallenge,
            nonce
        );

        events.CollectEvent(new GoogleSsoLoginStarted(resolved.OrgId));

        var httpContext = httpContextAccessor.HttpContext!;
        var returnPath = httpContext.Request.Query["ReturnPath"].ToString();
        if (!string.IsNullOrEmpty(returnPath))
        {
            ReturnPathHelper.SetReturnPathCookie(httpContext, returnPath);
        }

        return Result<string>.Redirect(authorizationUrl);
    }

    private static string BuildAuthorizationUrl(
        string clientId,
        string hostedDomain,
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
            ["prompt"] = "select_account",
            ["hd"] = hostedDomain
        };

        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{GoogleSsoConfigurator.GetAuthorizationEndpoint()}?{queryString}";
    }
}
