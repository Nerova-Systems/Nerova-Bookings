namespace Main.Features.Workflows.Senders;

/// <summary>
///     Outbound WhatsApp provider used by the WorkflowReminder dispatcher.
///     Meta WhatsApp Business requires a pre-approved <c>templateName</c> with named
///     <c>variables</c> substituted into the template body. See:
///     https://developers.facebook.com/docs/whatsapp/cloud-api/reference/messages
/// </summary>
public interface IWhatsAppProvider
{
    Task<WhatsAppResult> SendAsync(
        string toE164,
        string templateName,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken
    );
}

public sealed record WhatsAppResult(WhatsAppResultStatus Status, string? MessageId, string? ErrorReason)
{
    public static WhatsAppResult Sent(string messageId)
    {
        return new WhatsAppResult(WhatsAppResultStatus.Sent, messageId, null);
    }

    public static WhatsAppResult NotConfigured(string reason)
    {
        return new WhatsAppResult(WhatsAppResultStatus.NotConfigured, null, reason);
    }

    public static WhatsAppResult Transient(string reason)
    {
        return new WhatsAppResult(WhatsAppResultStatus.TransientFailure, null, reason);
    }

    public static WhatsAppResult Permanent(string reason)
    {
        return new WhatsAppResult(WhatsAppResultStatus.PermanentFailure, null, reason);
    }
}

public enum WhatsAppResultStatus
{
    Sent,
    NotConfigured,
    TransientFailure,
    PermanentFailure
}
