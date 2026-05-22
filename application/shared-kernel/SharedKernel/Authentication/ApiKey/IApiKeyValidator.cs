using System.Security.Claims;

namespace SharedKernel.Authentication.ApiKey;

/// <summary>
///     Validates a Nerova API key token and returns the resulting <see cref="ClaimsPrincipal" /> if valid.
///     Implementations are responsible for recording key usage (<c>MarkUsed</c>) inline or fire-and-forget.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    ///     Returns the authenticated <see cref="ClaimsPrincipal" /> for <paramref name="token" />,
    ///     or <see langword="null" /> if the token is invalid, expired, or revoked.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken = default);
}
