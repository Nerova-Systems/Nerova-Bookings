using Main.Features.Workflows.Senders;

namespace Main.Features.Workflows.Infrastructure;

public sealed class StubWhatsappSender : IWhatsappSender
{
    public Task<string?> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        // Stub: no-op until a real WhatsApp provider is integrated
        return Task.FromResult<string?>(null);
    }
}
