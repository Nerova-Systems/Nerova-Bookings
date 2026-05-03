using SharedKernel.Emails;

namespace Account.Features.EmailAuthentication.EmailTemplates;

public sealed record StartSignupEmailTemplate(string Locale, StartSignupEmailModel Data)
    : EmailTemplateBase("StartSignup", Locale, Data);

public sealed record StartSignupEmailModel(string OneTimePassword, string Domain, int ExpiryMinutes);
