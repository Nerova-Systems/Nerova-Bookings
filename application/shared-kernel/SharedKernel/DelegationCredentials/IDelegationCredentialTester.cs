using JetBrains.Annotations;

namespace SharedKernel.DelegationCredentials;

/// <summary>
///     Tests whether a delegation credential is valid by making a real API call to the
///     target workspace platform.
/// </summary>
/// <remarks>
///     The default registered implementation (<c>NotConfiguredDelegationCredentialTester</c>) is a
///     stub that always returns a failure. Replace it with real Google / Microsoft SDK
///     implementations in Wave 3+.
/// </remarks>
public interface IDelegationCredentialTester
{
    /// <summary>
    ///     Attempts to use <paramref name="keyBlob" /> to call the target platform on behalf of
    ///     <paramref name="memberEmail" /> and returns whether the credential is valid.
    /// </summary>
    Task<DelegationCredentialTestResult> TestAsync(
        string keyBlob,
        WorkspacePlatform platform,
        string memberEmail,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     The result of a delegation-credential connectivity test.
/// </summary>
[PublicAPI]
public sealed record DelegationCredentialTestResult(bool Success, string? Error);
