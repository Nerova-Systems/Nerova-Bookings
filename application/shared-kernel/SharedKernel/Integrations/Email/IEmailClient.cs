namespace SharedKernel.Integrations.Email;

public interface IEmailClient
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
