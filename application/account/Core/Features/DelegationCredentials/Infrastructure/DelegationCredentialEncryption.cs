using Microsoft.AspNetCore.DataProtection;

namespace Account.Features.DelegationCredentials.Infrastructure;

/// <summary>
///     Encrypts and decrypts delegation credential key blobs using ASP.NET Core Data Protection.
///     Stored key blobs are never readable without the application's key ring.
/// </summary>
public sealed class DelegationCredentialEncryption
{
    private readonly IDataProtector _protector;

    public DelegationCredentialEncryption(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("delegation-credential");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
