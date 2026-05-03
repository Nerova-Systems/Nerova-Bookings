using System.Text.RegularExpressions;
using HandlebarsDotNet;

namespace SharedKernel.Emails;

public sealed partial class HandlebarsEmailRenderer(IHandlebars handlebars, IEmailTemplateLoader templateLoader) : IEmailRenderer
{
    public EmailRenderResult RenderEmail(EmailTemplateBase template)
    {
        var htmlSource = templateLoader.LoadHtml(template.Name, template.Locale);
        var plainTextSource = templateLoader.LoadPlainText(template.Name, template.Locale);

        var htmlBody = handlebars.Compile(htmlSource)(template.Model);
        var plainTextBody = handlebars.Compile(plainTextSource)(template.Model);

        var subject = ExtractSubject(htmlBody, template.Name);
        return new EmailRenderResult(subject, htmlBody, plainTextBody);
    }

    private static string ExtractSubject(string htmlBody, string templateName)
    {
        var match = TitleRegex().Match(htmlBody);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Email template '{templateName}' is missing a <title> element required for the subject line.");
        }

        return WhitespaceRegex().Replace(match.Groups[1].Value, " ").Trim();
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
