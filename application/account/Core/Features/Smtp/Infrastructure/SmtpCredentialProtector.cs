using Microsoft.AspNetCore.DataProtection;

namespace Account.Features.Smtp.Infrastructure;

/// <summary>
///     Encrypts and decrypts SMTP passwords using ASP.NET Core Data Protection.
///     Stored passwords are never readable without the application's key ring.
/// </summary>
public sealed class SmtpCredentialProtector
{
    private readonly IDataProtector _protector;

    public SmtpCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("org-smtp-credentials");
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
