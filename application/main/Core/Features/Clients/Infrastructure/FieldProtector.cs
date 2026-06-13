using Microsoft.AspNetCore.DataProtection;

namespace Main.Features.Clients.Infrastructure;

/// <summary>
///     Encrypts and decrypts the sensitive vertical-field JSON payload stored on
///     <c>Client.SensitiveFields</c> (docs/vertical-template-fields-spec.md §3, §5) using ASP.NET Core
///     Data Protection — the <c>CredentialProtector</c> pattern. Sensitive values are never persisted
///     in cleartext, never logged, and never exposed to any agent tool.
/// </summary>
public sealed class FieldProtector(IDataProtectionProvider provider)
{
    private const string DataProtectionPurpose = "main.clients.sensitive-fields";
    private readonly IDataProtector _protector = provider.CreateProtector(DataProtectionPurpose);

    public string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        return _protector.Unprotect(ciphertext);
    }
}
