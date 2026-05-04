using SharedKernel.Emails;

namespace Account.Features.Users.EmailTemplates;

public sealed record InviteUserEmailTemplate(string Locale, InviteUserEmailModel Data)
    : EmailTemplateBase("InviteUser", Locale, Data);

public sealed record InviteUserEmailModel(string InviterName, string TenantName, string Email, string LoginUrl);
