using System.Globalization;

namespace Main.Features.Clients.Domain;

/// <summary>
///     Catalog-driven value validation for vertical fields (docs/vertical-template-fields-spec.md §5).
///     One validator for every field: the value is checked against the definition's
///     <see cref="VerticalFieldKind" /> (date parse, number, choice membership, text length ≤ 500 /
///     longtext ≤ 4000). Unknown keys are rejected. There are no per-field validator classes.
/// </summary>
public static class VerticalFieldValueValidator
{
    private const int MaxTextLength = 500;
    private const int MaxLongTextLength = 4000;

    /// <summary>Returns null when valid; otherwise a human-readable error for the given key/value pair.</summary>
    public static string? Validate(VerticalFieldDefinition definition, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null; // Clearing a field is always valid

        return definition.Kind switch
        {
            VerticalFieldKind.Text when value.Length > MaxTextLength
                => $"Value for '{definition.Key}' must be at most {MaxTextLength} characters.",
            VerticalFieldKind.LongText when value.Length > MaxLongTextLength
                => $"Value for '{definition.Key}' must be at most {MaxLongTextLength} characters.",
            VerticalFieldKind.Number when !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                => $"Value for '{definition.Key}' must be a number.",
            VerticalFieldKind.Date when !DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _)
                => $"Value for '{definition.Key}' must be a date (yyyy-MM-dd).",
            VerticalFieldKind.Boolean when !bool.TryParse(value, out _)
                => $"Value for '{definition.Key}' must be 'true' or 'false'.",
            VerticalFieldKind.Choice when !definition.Options.Contains(value, StringComparer.OrdinalIgnoreCase)
                => $"Value for '{definition.Key}' must be one of: {string.Join(", ", definition.Options)}.",
            VerticalFieldKind.MultiChoice when value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(option => !definition.Options.Contains(option, StringComparer.OrdinalIgnoreCase))
                => $"Values for '{definition.Key}' must be a comma-separated subset of: {string.Join(", ", definition.Options)}.",
            _ => null
        };
    }
}
