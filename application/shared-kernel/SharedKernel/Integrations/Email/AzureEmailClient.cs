using Azure;
using Azure.Communication.Email;
using Azure.Security.KeyVault.Secrets;

namespace SharedKernel.Integrations.Email;

public sealed class AzureEmailClient(SecretClient secretClient) : IEmailClient
{
    private const string SecretName = "communication-services-connection-string";

    private static readonly string Sender = Environment.GetEnvironmentVariable("SENDER_EMAIL_ADDRESS")!;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var connectionString = await secretClient.GetSecretAsync(SecretName, cancellationToken: cancellationToken);

        var emailClient = new EmailClient(connectionString.Value.Value);
        var content = new EmailContent(message.Subject) { Html = message.HtmlBody, PlainText = message.PlainTextBody };
        var azureMessage = new Azure.Communication.Email.EmailMessage(Sender, message.Recipient, content);

        if (message.Headers is not null)
        {
            foreach (var header in message.Headers)
            {
                azureMessage.Headers.Add(header.Key, header.Value);
            }
        }

        await emailClient.SendAsync(WaitUntil.Started, azureMessage, cancellationToken);
    }
}
