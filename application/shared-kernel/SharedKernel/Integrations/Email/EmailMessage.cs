namespace SharedKernel.Integrations.Email;

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    IReadOnlyDictionary<string, string>? Headers = null
);
