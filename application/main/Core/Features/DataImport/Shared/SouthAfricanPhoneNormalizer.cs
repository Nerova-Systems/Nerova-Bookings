using System.Text;

namespace Main.Features.DataImport.Shared;

/// <summary>
///     Normalizes South African phone numbers to E.164 (spec R19): handles +27/27/0 prefixes, spaces,
///     dashes, dots, and parentheses. Returns null when the value cannot be a valid SA number — the row
///     is then flagged rather than silently imported with a broken number.
/// </summary>
public static class SouthAfricanPhoneNormalizer
{
    public static string? Normalize(string? rawPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber)) return null;

        var digits = new StringBuilder();
        var hasLeadingPlus = false;
        foreach (var character in rawPhoneNumber.Trim())
        {
            if (character == '+' && digits.Length == 0)
            {
                hasLeadingPlus = true;
            }
            else if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
            else if (character is not (' ' or '-' or '.' or '(' or ')' or '/'))
            {
                return null;
            }
        }

        var number = digits.ToString();

        if (hasLeadingPlus)
        {
            // Already international: accept +27 followed by nine digits; pass through other country codes untouched.
            if (number.StartsWith("27", StringComparison.Ordinal) && number.Length == 11) return $"+{number}";
            return number.Length is >= 8 and <= 15 ? $"+{number}" : null;
        }

        return number switch
        {
            { Length: 11 } when number.StartsWith("27", StringComparison.Ordinal) => $"+{number}",
            { Length: 10 } when number.StartsWith('0') => $"+27{number[1..]}",
            { Length: 9 } when number[0] is '6' or '7' or '8' => $"+27{number}",
            _ => null
        };
    }
}
