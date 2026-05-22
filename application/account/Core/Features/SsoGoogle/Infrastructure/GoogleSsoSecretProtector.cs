using Microsoft.AspNetCore.DataProtection;

namespace Account.Features.SsoGoogle.Infrastructure;

/// <summary>
///     Encrypts and decrypts Google Workspace SSO provider config using ASP.NET Core Data Protection.
///     Stored config is never readable without the application's key ring.
/// </summary>
public sealed class GoogleSsoSecretProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("org-sso-google");

    public string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        return _protector.Unprotect(ciphertext);
    }
}
