namespace Main.Features.Workflows.Senders;

public interface ISmsSender
{
    Task<string?> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}
