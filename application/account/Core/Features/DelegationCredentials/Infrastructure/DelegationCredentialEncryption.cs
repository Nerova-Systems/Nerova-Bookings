using Microsoft.AspNetCore.DataProtection;

namespace Account.Features.DelegationCredentials.Infrastructure;

/// <summary>
///     Encrypts and decrypts delegation credential key blobs using ASP.NET Core Data Protection.
///     Stored key blobs are never readable without the application's key ring.
/// </summary>
public sealed class DelegationCredentialEncryption(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("delegation-credential");

    public string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        return _protector.Unprotect(ciphertext);
    }
}
