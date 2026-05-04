namespace SharedKernel.Emails;

public abstract record EmailTemplateBase(string Name, string Locale, object Model);
