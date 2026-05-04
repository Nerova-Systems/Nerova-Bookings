namespace SharedKernel.Emails;

public sealed record EmailRenderResult(string Subject, string HtmlBody, string PlainTextBody);
