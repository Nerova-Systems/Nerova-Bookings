using SharedKernel.Emails;

namespace Account.Features.EmailAuthentication.EmailTemplates;

public sealed record ResendEmailLoginEmailTemplate(string Locale, ResendEmailLoginEmailModel Data)
    : EmailTemplateBase("ResendEmailLogin", Locale, Data);

public sealed record ResendEmailLoginEmailModel(string OneTimePassword, string Domain, int ExpiryMinutes);
