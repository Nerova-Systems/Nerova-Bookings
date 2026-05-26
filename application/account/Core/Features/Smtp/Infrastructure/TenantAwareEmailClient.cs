using System.Net;
using System.Net.Mail;
using Account.Features.Smtp.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Integrations.Email;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Smtp.Infrastructure;

/// <summary>
///     Decorator for <see cref="IEmailClient" /> that routes outbound emails through an
///     organization's custom SMTP server when:
///     <list type="bullet">
///         <item>the request is in an active org context (<c>ActiveOrgId</c> is set),</item>
///         <item>the <c>cap-custom-smtp</c> feature flag is enabled for the user,</item>
///         <item>and an <see cref="OrgSmtpConfig" /> with <c>IsEnabled = true</c> exists.</item>
///     </list>
///     Falls back to the platform email client in all other cases.
/// </summary>
public sealed class TenantAwareEmailClient(
    IEmailClient platformClient,
    IOrgSmtpConfigRepository configRepository,
    IExecutionContext executionContext,
    SmtpCredentialProtector credentialProtector
) : IEmailClient
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var orgId = executionContext.ActiveOrgId;
        if (orgId is not null && executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapCustomSmtp.Key))
        {
            var config = await configRepository.GetByOrgIdAsync(orgId, cancellationToken);
            if (config is { IsEnabled: true })
            {
                await SendViaOrgSmtpAsync(message, config, cancellationToken);
                return;
            }
        }

        await platformClient.SendAsync(message, cancellationToken);
    }

    private async Task SendViaOrgSmtpAsync(EmailMessage message, OrgSmtpConfig config, CancellationToken cancellationToken)
    {
        var password = credentialProtector.Unprotect(config.EncryptedPassword);

#pragma warning disable SYSLIB0006 // SmtpClient is deprecated but no alternative ships with .NET BCL; revisit when project adopts MailKit.
        // ReSharper disable once UsingStatementResourceInitialization
        using var smtpClient = new SmtpClient(config.Host, config.Port)
        {
            EnableSsl = config.UseSsl,
            Credentials = new NetworkCredential(config.Username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
#pragma warning restore SYSLIB0006

        // ReSharper disable once UsingStatementResourceInitialization
        using var mailMessage = new MailMessage
        {
            From = new MailAddress(config.FromEmail, config.FromName ?? string.Empty),
            Subject = message.Subject,
            IsBodyHtml = true,
            Body = message.HtmlBody,
            To = { message.Recipient }
        };

        if (config.ReplyToEmail is not null)
        {
            mailMessage.ReplyToList.Add(new MailAddress(config.ReplyToEmail));
        }

        if (message.Headers is not null)
        {
            foreach (var (key, value) in message.Headers)
            {
                mailMessage.Headers.Add(key, value);
            }
        }

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}
