using SharedKernel.Emails;

namespace Account.Features.EmailAuthentication.EmailTemplates;

public sealed record UnknownUserEmailTemplate(string Locale, UnknownUserEmailModel Data)
    : EmailTemplateBase("UnknownUser", Locale, Data);

public sealed record UnknownUserEmailModel(string Email, string SignupUrl);
