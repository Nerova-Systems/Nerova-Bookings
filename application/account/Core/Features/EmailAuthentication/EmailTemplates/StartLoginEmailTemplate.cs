using SharedKernel.Emails;

namespace Account.Features.EmailAuthentication.EmailTemplates;

public sealed record StartLoginEmailTemplate(string Locale, StartLoginEmailModel Data)
    : EmailTemplateBase("StartLogin", Locale, Data);

public sealed record StartLoginEmailModel(string OneTimePassword, string Domain, int ExpiryMinutes);
