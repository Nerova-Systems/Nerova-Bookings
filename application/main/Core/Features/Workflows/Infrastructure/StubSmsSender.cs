using Main.Features.Workflows.Senders;

namespace Main.Features.Workflows.Infrastructure;

public sealed class StubSmsSender : ISmsSender
{
    public Task<string?> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        // Stub: no-op until a real SMS provider is integrated
        return Task.FromResult<string?>(null);
    }
}
