using System.Globalization;
using HandlebarsDotNet;
using Humanizer;

namespace SharedKernel.Emails;

internal static class EmailHelpers
{
    public static void Register(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("formatCurrency", FormatCurrency);
        handlebars.RegisterHelper("formatDate", FormatDate);
        handlebars.RegisterHelper("pluralize", Pluralize);
    }

    private static void FormatCurrency(EncodedTextWriter output, Context context, Arguments arguments)
    {
        // {{formatCurrency amount currency=string locale=string}}
        // Both currency and locale are required as hash arguments to make formatting explicit at the call site.
        var amount = ToDecimal(arguments[0]);
        var currency = RequireHashArgument(arguments, "currency", "formatCurrency");
        var locale = RequireHashArgument(arguments, "locale", "formatCurrency");

        var culture = CultureInfo.GetCultureInfo(locale);
        var numberFormat = (NumberFormatInfo)culture.NumberFormat.Clone();
        numberFormat.CurrencySymbol = ResolveCurrencySymbol(currency, culture);
        output.WriteSafeString(amount.ToString("C", numberFormat));
    }

    private static void FormatDate(EncodedTextWriter output, Context context, Arguments arguments)
    {
        // {{formatDate value locale=string format=string?}}
        // format is optional and defaults to the locale's long date pattern.
        var value = ToDateTimeOffset(arguments[0]);
        var locale = RequireHashArgument(arguments, "locale", "formatDate");
        var format = OptionalHashArgument(arguments, "format");

        var culture = CultureInfo.GetCultureInfo(locale);
        output.WriteSafeString(format is null ? value.ToString("D", culture) : value.ToString(format, culture));
    }

    private static void Pluralize(EncodedTextWriter output, Context context, Arguments arguments)
    {
        // {{pluralize count singular plural?}}
        // If plural is omitted, Humanizer derives it from the singular form.
        var count = ToInt(arguments[0]);
        var singular = arguments[1]?.ToString() ?? throw new ArgumentException("pluralize requires a singular form.");
        var plural = arguments.Length > 2 ? arguments[2]?.ToString() : null;

        var word = count == 1 ? singular : plural ?? singular.Pluralize(false);
        output.WriteSafeString(word);
    }

    private static string RequireHashArgument(Arguments arguments, string key, string helperName)
    {
        var value = OptionalHashArgument(arguments, key);
        if (value is null) throw new ArgumentException($"{helperName} requires a '{key}' hash argument.");
        return value;
    }

    private static string? OptionalHashArgument(Arguments arguments, string key)
    {
        return arguments.Hash.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static decimal ToDecimal(object? value)
    {
        return value switch
        {
            null => throw new ArgumentNullException(nameof(value), "Numeric value is required."),
            decimal d => d,
            IConvertible convertible => convertible.ToDecimal(CultureInfo.InvariantCulture),
            _ => decimal.Parse(value.ToString()!, CultureInfo.InvariantCulture)
        };
    }

    private static int ToInt(object? value)
    {
        return value switch
        {
            null => throw new ArgumentNullException(nameof(value), "Count value is required."),
            int i => i,
            IConvertible convertible => convertible.ToInt32(CultureInfo.InvariantCulture),
            _ => int.Parse(value.ToString()!, CultureInfo.InvariantCulture)
        };
    }

    private static DateTimeOffset ToDateTimeOffset(object? value)
    {
        return value switch
        {
            null => throw new ArgumentNullException(nameof(value), "Date value is required."),
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            DateOnly date => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => throw new ArgumentException($"Cannot convert value of type '{value.GetType().Name}' to a date.", nameof(value))
        };
    }

    private static string ResolveCurrencySymbol(string currencyCode, CultureInfo culture)
    {
        // Match the symbol shown by RegionInfo when the culture's currency matches; otherwise fall back to
        // the ISO code so EUR formatted with en-US shows as "EUR 12.34" instead of mis-labelling as "$".
        try
        {
            var region = new RegionInfo(culture.Name);
            if (string.Equals(region.ISOCurrencySymbol, currencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return region.CurrencySymbol;
            }
        }
        catch (ArgumentException)
        {
            // Neutral or invalid culture; fall through.
        }

        return currencyCode.ToUpperInvariant();
    }
}
