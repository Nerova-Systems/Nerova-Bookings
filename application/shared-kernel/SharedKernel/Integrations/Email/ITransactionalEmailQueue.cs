namespace SharedKernel.Integrations.Email;

public interface ITransactionalEmailQueue
{
    Task EnqueueAsync(
        string recipient,
        string subject,
        string htmlContent,
        string templateKey,
        string? correlationId,
        CancellationToken cancellationToken
    );
}
