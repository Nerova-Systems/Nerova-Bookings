using Main.Features.Workflows.Senders;
using SharedKernel.Domain;

namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Stub host email provider.
///     TODO: Integrate with the Account service to resolve user emails cross-service.
/// </summary>
public sealed class StubHostEmailProvider(ILogger<StubHostEmailProvider> logger) : IHostEmailProvider
{
    public Task<string?> GetEmailAsync(UserId userId, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Host email resolution for user {UserId} is not yet implemented. EmailHost workflow actions will be skipped.",
            userId.Value
        );
        return Task.FromResult<string?>(null);
    }
}
