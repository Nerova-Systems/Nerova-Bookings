using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Account.Features.AttributeSync.Domain;
using Account.Features.Authentication.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Sso.Domain;
using Account.Features.Sso.Events;
using Account.Features.SsoGoogle.Infrastructure;
using Account.Features.Users.Domain;
using Account.Features.Users.Shared;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Authentication.TokenGeneration;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.OpenIdConnect;
using SharedKernel.Telemetry;

namespace Account.Features.SsoGoogle.Commands;

/// <summary>
///     Handles the OAuth 2.0 authorization code callback from Google for SSO login.
///     Validates the state + cookie, exchanges the code for tokens, validates the ID token,
///     verifies the <c>hd</c> claim (Google Workspace hosted domain), verifies the user's email
///     domain, resolves the org user, and issues a session.
/// </summary>
[PublicAPI]
public sealed record CompleteGoogleSsoCommand(
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription
) : ICommand, IRequest<Result<string>>;

public sealed class CompleteGoogleSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    GoogleSsoConfigurator configurator,
    GoogleSsoStateService ssoStateService,
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    UserInfoFactory userInfoFactory,
    AuthenticationTokenService authenticationTokenService,
    OpenIdConnectConfigurationManagerFactory configManagerFactory,
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    IMembershipRepository membershipRepository,
    IPublisher publisher,
    ILogger<CompleteGoogleSsoHandler> logger
) : IRequestHandler<CompleteGoogleSsoCommand, Result<string>>
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public async Task<Result<string>> Handle(CompleteGoogleSsoCommand command, CancellationToken cancellationToken)
    {
        var stateCookie = ssoStateService.GetStateCookie();

        try
        {
            if (!string.IsNullOrEmpty(command.Error))
            {
                logger.LogWarning("Google SSO error from identity provider: {Error} - {ErrorDescription}", command.Error, command.ErrorDescription);
                return Fail("authentication_failed", stateCookie?.OrgId);
            }

            // Validate that state token + cookie are present and consistent.
            var orgIdFromState = ssoStateService.GetOrgIdFromStateToken(command.State);
            if (orgIdFromState is null || stateCookie is null)
            {
                logger.LogWarning("Google SSO callback missing state or cookie");
                return Fail("invalid_request");
            }

            if (orgIdFromState != stateCookie.OrgId)
            {
                logger.LogWarning("Google SSO state/cookie org ID mismatch");
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            if (!ssoStateService.ValidateBrowserFingerprint(stateCookie.FingerprintHash))
            {
                logger.LogWarning("Google SSO browser fingerprint mismatch");
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            // Load the SSO config — SSO must still be enabled when the callback arrives.
            var config = await ssoConfigRepository.GetByOrgAndProviderAsync(stateCookie.OrgId, SsoProvider.Google, cancellationToken);
            if (config is null || !config.IsEnabled)
            {
                logger.LogWarning("Google SSO config not found or disabled for org {OrgId}", stateCookie.OrgId);
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            var resolved = configurator.DecryptConfig(config)!;

            // Exchange code for tokens.
            if (string.IsNullOrEmpty(command.Code))
            {
                return Fail("invalid_request", stateCookie.OrgId);
            }

            var tokenResponse = await ExchangeCodeAsync(resolved, command.Code, stateCookie.CodeVerifier, cancellationToken);
            if (tokenResponse is null)
            {
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            // Validate ID token and extract email + hd claim.
            var (userEmail, hostedDomainClaim, tokenClaims) = await ValidateIdTokenAndGetClaimsAsync(
                tokenResponse.IdToken, resolved, stateCookie.Nonce, cancellationToken
            );

            if (userEmail is null)
            {
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            // Google Workspace `hd` claim enforcement.
            // Personal Google accounts will never have this claim — they must be rejected.
            if (string.IsNullOrEmpty(hostedDomainClaim)
                || !hostedDomainClaim.Equals(resolved.HostedDomain, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Google SSO login rejected: hd claim '{Hd}' does not match expected '{Expected}' for org {OrgId}",
                    hostedDomainClaim ?? "(absent)", resolved.HostedDomain, stateCookie.OrgId
                );
                return Fail("user_not_found", stateCookie.OrgId);
            }

            // Enforce allowed domain restriction.
            var domain = ExtractEmailDomain(userEmail);
            if (!resolved.AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("Google SSO login rejected: domain '{Domain}' not in allowed list for org {OrgId}", domain, stateCookie.OrgId);
                return Fail("user_not_found", stateCookie.OrgId);
            }

            // Resolve a user in this org matching the email.
            var usersWithEmail = await userRepository.GetUsersByEmailUnfilteredAsync(userEmail, cancellationToken);
            var user = usersWithEmail.FirstOrDefault(u => u.TenantId == stateCookie.OrgId);
            if (user is null)
            {
                logger.LogWarning("Google SSO: no user with email '{Email}' found in org {OrgId}", userEmail, stateCookie.OrgId);
                return Fail("user_not_found", stateCookie.OrgId);
            }

            // Create session.
            var httpContext = httpContextAccessor.HttpContext!;
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = executionContext.ClientIpAddress;
            var session = Session.Create(user.TenantId, user.Id, LoginMethod.GoogleSso, userAgent, ipAddress);
            await sessionRepository.AddAsync(session, cancellationToken);

            user.UpdateLastSeen(timeProvider.GetUtcNow());
            userRepository.Update(user);

            var userInfoResult = await userInfoFactory.CreateUserInfoAsync(user, session.Id, cancellationToken);
            if (!userInfoResult.IsSuccess) return Result<string>.From(userInfoResult);

            authenticationTokenService.CreateAndSetAuthenticationTokens(userInfoResult.Value!, session.Id, session.RefreshTokenJti);

            // Publish SSO login event so attribute sync rules are applied.
            // Failures in the handler must never block SSO login — the handler catches internally.
            var membership = await membershipRepository.GetByUserAndTenantAsync(user.Id, stateCookie.OrgId, cancellationToken);
            if (membership is not null)
            {
                await publisher.Publish(
                    new SsoLoginCompletedEvent(membership.Id, stateCookie.OrgId, SyncSource.GoogleSso, tokenClaims),
                    cancellationToken
                );
            }
            else
            {
                logger.LogWarning("Google SSO: membership not found for user {UserId} in org {OrgId} — skipping attribute sync", user.Id, stateCookie.OrgId);
            }

            events.CollectEvent(new SessionCreated(session.Id));
            events.CollectEvent(new GoogleSsoLoginSucceeded(user.Id, stateCookie.OrgId));

            var returnPath = ReturnPathHelper.GetReturnPathCookie(httpContext) ?? "/";
            ReturnPathHelper.ClearReturnPathCookie(httpContext);

            return Result<string>.Redirect(returnPath);
        }
        finally
        {
            ssoStateService.ClearStateCookie();
        }
    }

    private async Task<GoogleTokenResponse?> ExchangeCodeAsync(
        ResolvedGoogleSsoConfig resolved,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokenEndpoint = GoogleSsoConfigurator.GetTokenEndpoint();
            var redirectUri = GoogleSsoStateService.GetCallbackUrl();

            var content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", resolved.ClientId),
                    new KeyValuePair<string, string>("client_secret", resolved.ClientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier)
                ]
            );

            var httpClient = httpClientFactory.CreateClient("google-sso");
            var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Google SSO token exchange failed with status '{StatusCode}': {ErrorBody}",
                    response.StatusCode, errorBody.Length > 500 ? errorBody[..500] : errorBody
                );
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google SSO token exchange HTTP error");
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Google SSO token exchange timed out");
            return null;
        }
    }

    private async Task<(string? Email, string? HostedDomain, IReadOnlyDictionary<string, JsonElement> Claims)> ValidateIdTokenAndGetClaimsAsync(
        string? idToken,
        ResolvedGoogleSsoConfig resolved,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        var emptyClaims = (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>();
        if (string.IsNullOrEmpty(idToken)) return (null, null, emptyClaims);
        if (!TokenHandler.CanReadToken(idToken)) return (null, null, emptyClaims);

        var configManager = configManagerFactory.GetOrCreate(GoogleSsoConfigurator.GoogleDiscoveryUrl);
        var openIdConfig = await configManager.GetConfigurationAsync(cancellationToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // Google accepts both issuer formats — https://accounts.google.com and accounts.google.com
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidateAudience = true,
            ValidAudiences = [resolved.ClientId],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(10),
            IssuerSigningKeys = openIdConfig.SigningKeys,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        var validationResult = await TokenHandler.ValidateTokenAsync(idToken, validationParameters);
        if (!validationResult.IsValid)
        {
            logger.LogError(validationResult.Exception, "Google SSO ID token validation failed");
            return (null, null, emptyClaims);
        }

        var token = (JsonWebToken)validationResult.SecurityToken;

        // Validate nonce to prevent replay attacks.
        var tokenNonce = token.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (tokenNonce != expectedNonce)
        {
            logger.LogWarning("Google SSO nonce mismatch");
            return (null, null, emptyClaims);
        }

        var email = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            logger.LogWarning("Google SSO ID token missing valid email claim");
            return (null, null, emptyClaims);
        }

        // `hd` claim = hosted domain for Google Workspace users.
        // Personal Google accounts do NOT have this claim.
        var hd = token.Claims.FirstOrDefault(c => c.Type == "hd")?.Value;

        return (email.ToLowerInvariant(), hd, BuildClaimsDictionary(token.Claims));
    }

    private static IReadOnlyDictionary<string, JsonElement> BuildClaimsDictionary(IEnumerable<Claim> claims)
    {
        return claims
            .GroupBy(c => c.Type)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var values = g.Select(c => c.Value).ToArray();
                    return values.Length == 1
                        ? JsonSerializer.SerializeToElement(values[0])
                        : JsonSerializer.SerializeToElement(values);
                }
            );
    }

    private Result<string> Fail(string reason, TenantId? orgId = null)
    {
        if (orgId is not null) events.CollectEvent(new GoogleSsoLoginFailed(orgId, reason));
        return Result<string>.Redirect($"/error?error={reason}");
    }

    private static string ExtractEmailDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..] : string.Empty;
    }

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("id_token")]
        string? IdToken,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn,
        [property: JsonPropertyName("token_type")]
        string TokenType
    );
}
