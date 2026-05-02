using System.Net.Mail;
using SharedKernel.Configuration;

namespace SharedKernel.Integrations.Email;

public sealed class DevelopmentEmailClient(PortAllocation ports) : IEmailClient
{
    private const string Sender = "no-reply@localhost";

    private readonly SmtpClient _emailSender = new("localhost", ports.MailpitSmtp);

    public Task SendAsync(string recipient, string subject, string htmlContent, CancellationToken cancellationToken)
    {
        var mailMessage = new MailMessage(Sender, recipient, subject, htmlContent) { IsBodyHtml = true };
        return _emailSender.SendMailAsync(mailMessage, cancellationToken);
    }
}
