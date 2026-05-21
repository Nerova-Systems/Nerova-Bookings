using Account.Features.DelegationCredentials.Domain;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;

namespace Account.Features.DelegationCredentials.Infrastructure;

/// <summary>
///     Resolves a decrypted delegation credential for an org member by matching the member's
///     email domain against the credential's configured domain.
/// </summary>
public sealed class DelegationCredentialResolver(
    IDelegationCredentialRepository repository,
    DelegationCredentialEncryption encryption) : IDelegationCredentialResolver
{
    public async Task<ResolvedCredential?> ResolveAsync(
        TenantId orgTenantId,
        string memberEmail,
        WorkspacePlatform platform,
        CancellationToken cancellationToken = default)
    {
        var credential = await repository.GetByOrgAndPlatformAsync(orgTenantId, platform, cancellationToken);

        if (credential is null || credential.Status != DelegationCredentialStatus.Active)
            return null;

        var atIndex = memberEmail.IndexOf('@');
        if (atIndex < 0) return null;

        var emailDomain = memberEmail[(atIndex + 1)..];
        if (!string.Equals(emailDomain, credential.Domain, StringComparison.OrdinalIgnoreCase))
            return null;

        var keyBlob = encryption.Unprotect(credential.EncryptedKeyBlob);
        return new ResolvedCredential(platform, keyBlob, memberEmail, credential.Domain);
    }
}
