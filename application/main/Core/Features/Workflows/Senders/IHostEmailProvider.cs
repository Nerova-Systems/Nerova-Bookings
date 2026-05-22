using SharedKernel.Domain;

namespace Main.Features.Workflows.Senders;

/// <summary>
///     Resolves the email address for a booking host (owner).
///     Returns null when the email cannot be resolved (e.g., cross-service boundary not yet integrated).
/// </summary>
public interface IHostEmailProvider
{
    Task<string?> GetEmailAsync(UserId userId, CancellationToken cancellationToken);
}
