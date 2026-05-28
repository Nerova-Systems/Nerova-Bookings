namespace Account.Features.Tenants.Domain;

/// <summary>
///     Derives the <see cref="MetaBusinessVertical" /> from a free-text business category string
///     supplied during tenant onboarding. Matching is case-insensitive and substring-based so that
///     values like "Hair Salon", "BEAUTY SPA", or "dental clinic" all resolve without requiring the
///     caller to normalise the input.
/// </summary>
public static class VerticalDeriver
{
    public static MetaBusinessVertical DeriveFrom(string? businessCategory)
    {
        if (string.IsNullOrWhiteSpace(businessCategory)) return MetaBusinessVertical.Other;

        var lower = businessCategory.ToLowerInvariant();

        if (lower.Contains("salon") || lower.Contains("barber") || lower.Contains("beauty") || lower.Contains("hair"))
            return MetaBusinessVertical.Beauty;

        if (lower.Contains("tutor") || lower.Contains("education") || lower.Contains("school"))
            return MetaBusinessVertical.Education;

        if (lower.Contains("clinic") || lower.Contains("medical") || lower.Contains("health")
            || lower.Contains("doctor") || lower.Contains("dentist"))
            return MetaBusinessVertical.Health;

        if (lower.Contains("trainer") || lower.Contains("gym") || lower.Contains("fitness")
            || lower.Contains("personal training"))
            return MetaBusinessVertical.ProfessionalServices;

        if (lower.Contains("restaurant") || lower.Contains("cafe") || lower.Contains("food"))
            return MetaBusinessVertical.Restaurant;

        if (lower.Contains("retail") || lower.Contains("shop") || lower.Contains("store"))
            return MetaBusinessVertical.Retail;

        if (lower.Contains("travel") || lower.Contains("accommodation") || lower.Contains("hotel"))
            return MetaBusinessVertical.Travel;

        return MetaBusinessVertical.Other;
    }
}
