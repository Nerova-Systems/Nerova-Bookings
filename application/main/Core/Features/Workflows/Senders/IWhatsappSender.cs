namespace Main.Features.Workflows.Senders;

public interface IWhatsappSender
{
    Task<string?> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}
