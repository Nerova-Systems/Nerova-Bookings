using SharedKernel.DelegationCredentials;

namespace Account.Features.DelegationCredentials.Infrastructure;

/// <summary>
///     Stub implementation of <see cref="IDelegationCredentialTester" /> used until the real
///     Google and Microsoft SDK integrations are built (Wave 3+).
///     Always returns a failure with a "not configured" message so that the Test endpoint
///     correctly communicates that live validation is not yet available.
/// </summary>
/// <remarks>
///     TODO (Wave 3): Replace with real implementations that call Google Directory API
///     and Microsoft Graph to verify the service-account / OAuth credentials.
/// </remarks>
public sealed class NotConfiguredDelegationCredentialTester : IDelegationCredentialTester
{
    public Task<DelegationCredentialTestResult> TestAsync(
        string keyBlob,
        WorkspacePlatform platform,
        string memberEmail,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DelegationCredentialTestResult(
            Success: false,
            Error: "Delegation credential testing is not yet configured. Real testing requires Wave 3 SDK integration."));
    }
}
