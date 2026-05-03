namespace SharedKernel.Emails;

public interface IEmailTemplateLoader
{
    string LoadHtml(string name, string locale);

    string LoadPlainText(string name, string locale);
}
