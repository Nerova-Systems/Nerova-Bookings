using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using SharedKernel.Domain;
using SharedKernel.SinglePageApp;

namespace Account.Features.SsoGoogle;

public sealed record GoogleSsoStateCookie(TenantId OrgId, string FingerprintHash, string CodeVerifier, string Nonce);

/// <summary>
///     Manages the <c>__Host-sso-google-state</c> cookie used to carry SSO flow state
///     (org ID, browser fingerprint hash, PKCE code verifier, and nonce) across the redirect.
///     All values are encrypted using ASP.NET Core Data Protection.
/// </summary>
public sealed class GoogleSsoStateService(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider dataProtectionProvider, ILogger<GoogleSsoStateService> logger)
{
    private const string DataProtectionPurpose = "SsoLogin.Google";
    private const string CookieName = "__Host-sso-google-state";

    // Valid for 10 minutes — enough for the redirect round-trip, short enough to limit exposure.
    private const int ValidForSeconds = 600;

    private static readonly string PublicUrl = Environment.GetEnvironmentVariable("OAUTH_PUBLIC_URL")
                                               ?? Environment.GetEnvironmentVariable(SinglePageAppConfiguration.PublicUrlKey)
                                               ?? throw new InvalidOperationException($"'{SinglePageAppConfiguration.PublicUrlKey}' environment variable is not configured.");

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public static string GetCallbackUrl()
    {
        return $"{PublicUrl}/api/account/sso/google/callback";
    }

    /// <summary>Writes the encrypted SSO state cookie to the response.</summary>
    public void SetStateCookie(TenantId orgId, string fingerprintHash, string codeVerifier, string nonce)
    {
        var raw = $"{orgId}|{fingerprintHash}|{codeVerifier}|{nonce}";
        var encrypted = _dataProtector.Protect(raw);

        httpContextAccessor.HttpContext!.Response.Cookies.Append(
            CookieName,
            encrypted,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                MaxAge = TimeSpan.FromSeconds(ValidForSeconds)
            }
        );
    }

    /// <summary>
    ///     Reads and decrypts the SSO state cookie. Returns <see langword="null" /> if the cookie is
    ///     absent, tampered with, or expired.
    /// </summary>
    public GoogleSsoStateCookie? GetStateCookie()
    {
        var cookieValue = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookieValue)) return null;

        try
        {
            var raw = _dataProtector.Unprotect(cookieValue);
            var parts = raw.Split('|');
            if (parts.Length != 4) return null;

            if (!TenantId.TryParse(parts[0], out var orgId)) return null;

            return new GoogleSsoStateCookie(orgId, parts[1], parts[2], parts[3]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt Google SSO state cookie");
            return null;
        }
    }

    /// <summary>Removes the SSO state cookie from the response.</summary>
    public void ClearStateCookie()
    {
        httpContextAccessor.HttpContext!.Response.Cookies.Delete(CookieName, new CookieOptions { Secure = true });
    }

    /// <summary>
    ///     Generates a SHA-256 hash of User-Agent and Accept-Language headers as a low-entropy
    ///     browser fingerprint.
    /// </summary>
    public string GenerateBrowserFingerprintHash()
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var acceptLanguage = httpContext.Request.Headers.AcceptLanguage.ToString();
        var fingerprint = $"{userAgent}|{acceptLanguage}";
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }

    /// <summary>
    ///     Validates that the current browser fingerprint matches the one stored at initiation.
    ///     Skipped in test environments where the mock provider is active.
    /// </summary>
    public bool ValidateBrowserFingerprint(string storedHash)
    {
        return GenerateBrowserFingerprintHash() == storedHash;
    }

    /// <summary>Protects the org tenant ID into an opaque CSRF state token for the authorization URL.</summary>
    public string ProtectStateToken(TenantId orgId)
    {
        return _dataProtector.Protect(orgId.ToString());
    }

    /// <summary>Extracts the org tenant ID from the CSRF state token returned by Google.</summary>
    public TenantId? GetOrgIdFromStateToken(string? state)
    {
        if (string.IsNullOrEmpty(state)) return null;

        try
        {
            var raw = _dataProtector.Unprotect(state);
            return TenantId.TryParse(raw, out var id) ? id : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt Google SSO state token");
            return null;
        }
    }
}
