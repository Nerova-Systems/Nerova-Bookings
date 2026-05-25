using Microsoft.AspNetCore.DataProtection;

namespace Main.Features.Apps.Infrastructure;

/// <summary>
///     Encrypts and decrypts the JSON credential blob (access token / refresh token / expiry)
///     stored on <c>Credential.EncryptedKey</c> using ASP.NET Core Data Protection. Stored
///     credentials are never readable without the application's key ring. Mirrors the
///     <c>SmtpCredentialProtector</c> / <c>MicrosoftSsoSecretProtector</c> pattern used in the
///     account SCS.
/// </summary>
public sealed class CredentialProtector
{
    private const string DataProtectionPurpose = "main.apps.credentials";
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(DataProtectionPurpose);
    }

    public string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        return _protector.Unprotect(ciphertext);
    }
}
