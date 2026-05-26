using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Account.Features.AttributeSync.Domain;
using Account.Features.Authentication.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Sso.Domain;
using Account.Features.Sso.Events;
using Account.Features.SsoMicrosoft.Infrastructure;
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

namespace Account.Features.SsoMicrosoft.Commands;

/// <summary>
///     Handles the OAuth 2.0 authorization code callback from Microsoft for SSO login.
///     Validates the state + cookie, exchanges the code for tokens, validates the ID token,
///     verifies the user's email domain, resolves the org user, and issues a session.
/// </summary>
[PublicAPI]
public sealed record CompleteMicrosoftSsoCommand(
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription
) : ICommand, IRequest<Result<string>>;

public sealed class CompleteMicrosoftSsoHandler(
    IOrgSsoConfigRepository ssoConfigRepository,
    MicrosoftSsoConfigurator configurator,
    SsoStateService ssoStateService,
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
    ILogger<CompleteMicrosoftSsoHandler> logger
) : IRequestHandler<CompleteMicrosoftSsoCommand, Result<string>>
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public async Task<Result<string>> Handle(CompleteMicrosoftSsoCommand command, CancellationToken cancellationToken)
    {
        var stateCookie = ssoStateService.GetStateCookie();

        try
        {
            if (!string.IsNullOrEmpty(command.Error))
            {
                logger.LogWarning("Microsoft SSO error from identity provider: {Error} - {ErrorDescription}", command.Error, command.ErrorDescription);
                return Fail("authentication_failed", stateCookie?.OrgId);
            }

            // Validate that state token + cookie are present and consistent.
            var orgIdFromState = ssoStateService.GetOrgIdFromStateToken(command.State);
            if (orgIdFromState is null || stateCookie is null)
            {
                logger.LogWarning("Microsoft SSO callback missing state or cookie");
                return Fail("invalid_request");
            }

            if (orgIdFromState != stateCookie.OrgId)
            {
                logger.LogWarning("Microsoft SSO state/cookie org ID mismatch");
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            if (!ssoStateService.ValidateBrowserFingerprint(stateCookie.FingerprintHash))
            {
                logger.LogWarning("Microsoft SSO browser fingerprint mismatch");
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            // Load the SSO config — SSO must still be enabled when the callback arrives.
            var config = await ssoConfigRepository.GetByOrgAndProviderAsync(stateCookie.OrgId, SsoProvider.Microsoft, cancellationToken);
            if (config is null || !config.IsEnabled)
            {
                logger.LogWarning("Microsoft SSO config not found or disabled for org {OrgId}", stateCookie.OrgId);
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

            // Validate ID token.
            var (userEmail, tokenClaims) = await ValidateIdTokenAndGetEmailAsync(
                tokenResponse.IdToken, resolved, stateCookie.Nonce, cancellationToken
            );

            if (userEmail is null)
            {
                return Fail("authentication_failed", stateCookie.OrgId);
            }

            // Enforce allowed domain restriction.
            var domain = ExtractEmailDomain(userEmail);
            if (!resolved.AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("Microsoft SSO login rejected: domain '{Domain}' not in allowed list for org {OrgId}", domain, stateCookie.OrgId);
                return Fail("user_not_found", stateCookie.OrgId);
            }

            // Resolve a user in this org matching the email.
            var usersWithEmail = await userRepository.GetUsersByEmailUnfilteredAsync(userEmail, cancellationToken);
            var user = usersWithEmail.FirstOrDefault(u => u.TenantId == stateCookie.OrgId);
            if (user is null)
            {
                logger.LogWarning("Microsoft SSO: no user with email '{Email}' found in org {OrgId}", userEmail, stateCookie.OrgId);
                return Fail("user_not_found", stateCookie.OrgId);
            }

            // Create session.
            var httpContext = httpContextAccessor.HttpContext!;
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = executionContext.ClientIpAddress;
            var session = Session.Create(user.TenantId, user.Id, LoginMethod.MicrosoftSso, userAgent, ipAddress);
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
                    new SsoLoginCompletedEvent(membership.Id, stateCookie.OrgId, SyncSource.MicrosoftSso, tokenClaims),
                    cancellationToken
                );
            }
            else
            {
                logger.LogWarning("Microsoft SSO: membership not found for user {UserId} in org {OrgId} — skipping attribute sync", user.Id, stateCookie.OrgId);
            }

            events.CollectEvent(new SessionCreated(session.Id));
            events.CollectEvent(new MicrosoftSsoLoginSucceeded(user.Id, stateCookie.OrgId));

            var returnPath = ReturnPathHelper.GetReturnPathCookie(httpContext) ?? "/";
            ReturnPathHelper.ClearReturnPathCookie(httpContext);

            return Result<string>.Redirect(returnPath);
        }
        finally
        {
            ssoStateService.ClearStateCookie();
        }
    }

    private async Task<MicrosoftTokenResponse?> ExchangeCodeAsync(
        ResolvedMicrosoftSsoConfig resolved,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokenEndpoint = MicrosoftSsoConfigurator.GetTokenEndpoint(resolved.AzureTenantId);
            var redirectUri = SsoStateService.GetCallbackUrl();

            var content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", resolved.ClientId),
                    new KeyValuePair<string, string>("client_secret", resolved.ClientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier)
                ]
            );

            var httpClient = httpClientFactory.CreateClient("microsoft-sso");
            var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Microsoft SSO token exchange failed with status '{StatusCode}': {ErrorBody}",
                    response.StatusCode, errorBody.Length > 500 ? errorBody[..500] : errorBody
                );
                return null;
            }

            return await response.Content.ReadFromJsonAsync<MicrosoftTokenResponse>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Microsoft SSO token exchange HTTP error");
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Microsoft SSO token exchange timed out");
            return null;
        }
    }

    private async Task<(string? Email, IReadOnlyDictionary<string, JsonElement> Claims)> ValidateIdTokenAndGetEmailAsync(
        string? idToken,
        ResolvedMicrosoftSsoConfig resolved,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(idToken)) return (null, new Dictionary<string, JsonElement>());
        if (!TokenHandler.CanReadToken(idToken)) return (null, new Dictionary<string, JsonElement>());

        var discoveryUrl = MicrosoftSsoConfigurator.GetDiscoveryUrl(resolved.AzureTenantId);
        var configManager = configManagerFactory.GetOrCreate(discoveryUrl);
        var openIdConfig = await configManager.GetConfigurationAsync(cancellationToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = [MicrosoftSsoConfigurator.GetAuthorityUrl(resolved.AzureTenantId)],
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
            logger.LogError(validationResult.Exception, "Microsoft SSO ID token validation failed");
            return (null, new Dictionary<string, JsonElement>());
        }

        var token = (JsonWebToken)validationResult.SecurityToken;

        // Validate nonce to prevent replay attacks.
        var tokenNonce = token.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (tokenNonce != expectedNonce)
        {
            logger.LogWarning("Microsoft SSO nonce mismatch");
            return (null, new Dictionary<string, JsonElement>());
        }

        // Microsoft uses preferred_username (UPN) as the primary email-like identifier;
        // fall back to the email claim when present.
        var email = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                    ?? token.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;

        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            logger.LogWarning("Microsoft SSO ID token missing valid email identifier");
            return (null, new Dictionary<string, JsonElement>());
        }

        return (email.ToLowerInvariant(), BuildClaimsDictionary(token.Claims));
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
        if (orgId is not null) events.CollectEvent(new MicrosoftSsoLoginFailed(orgId, reason));
        return Result<string>.Redirect($"/error?error={reason}");
    }

    private static string ExtractEmailDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..] : string.Empty;
    }

    private sealed record MicrosoftTokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("id_token")]
        string? IdToken,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn,
        [property: JsonPropertyName("token_type")]
        string TokenType,
        [property: JsonPropertyName("scope")] string Scope
    );
}
