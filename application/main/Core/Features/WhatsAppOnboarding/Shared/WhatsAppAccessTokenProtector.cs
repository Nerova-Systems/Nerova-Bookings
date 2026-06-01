using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace Main.Features.WhatsAppOnboarding.Shared;

/// <summary>
///     Encrypts and decrypts the Meta WhatsApp business access token so it is never stored in plaintext.
///     Uses the cross-SCS data protection provider (the same mechanism as external-login state) with a
///     dedicated purpose string so tokens cannot be unprotected by other protectors.
/// </summary>
public sealed class WhatsAppAccessTokenProtector(IDataProtectionProvider dataProtectionProvider, ILogger<WhatsAppAccessTokenProtector> logger)
{
    private const string DataProtectionPurpose = "WhatsAppAccessToken";

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

    public string Protect(string accessToken)
    {
        return _dataProtector.Protect(accessToken);
    }

    public string? Unprotect(string encryptedAccessToken)
    {
        try
        {
            return _dataProtector.Unprotect(encryptedAccessToken);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Failed to decrypt WhatsApp access token");
            return null;
        }
    }
}
