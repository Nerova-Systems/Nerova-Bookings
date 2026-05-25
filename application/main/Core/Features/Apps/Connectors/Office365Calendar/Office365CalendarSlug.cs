using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>Shared constants for the Microsoft Office 365 (Outlook) Calendar connector.</summary>
public static class Office365CalendarSlug
{
    public const string Value = "office365-calendar";

    public static readonly AppSlug Slug = new(Value);

    public const string HttpClientName = "office365-calendar";
}
