using SharedKernel.Domain;

namespace SharedKernel.DelegationCredentials;

/// <summary>
///     Resolves a decrypted delegation credential for an org member.
///     Used by calendar and conferencing services (Wave 3+) to obtain the key material
///     needed to call Google Calendar or Microsoft Graph on behalf of an org member.
/// </summary>
public interface IDelegationCredentialResolver
{
    /// <summary>
    ///     Returns a <see cref="ResolvedCredential" /> if the org has an active delegation credential
    ///     for <paramref name="platform" /> whose domain matches the domain part of
    ///     <paramref name="memberEmail" />, or <see langword="null" /> if no matching credential exists.
    /// </summary>
    Task<ResolvedCredential?> ResolveAsync(
        TenantId orgTenantId,
        string memberEmail,
        WorkspacePlatform platform,
        CancellationToken cancellationToken = default);
}
