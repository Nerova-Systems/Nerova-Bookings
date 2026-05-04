namespace SharedKernel.Emails;

public interface IEmailRenderer
{
    EmailRenderResult RenderEmail(EmailTemplateBase template);
}
