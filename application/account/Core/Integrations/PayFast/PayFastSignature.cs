using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Account.Integrations.PayFast;

public static class PayFastSignature
{
    // PayFast verifies onsite/process signatures using:
    //  1. The canonical field order below (NOT alphabetical) — this is the order from the official PayFast PHP SDK.
    //  2. PHP urlencode encoding (space → '+', not '%20'), via WebUtility.UrlEncode.
    //  3. PHP empty()/trim() semantics: skip null, empty, or whitespace values.
    private static readonly string[] CanonicalFieldOrder =
    [
        "merchant_id", "merchant_key", "return_url", "cancel_url", "notify_url",
        "name_first", "name_last", "email_address", "cell_number",
        "m_payment_id", "amount", "item_name", "item_description",
        "custom_int1", "custom_int2", "custom_int3", "custom_int4", "custom_int5",
        "custom_str1", "custom_str2", "custom_str3", "custom_str4", "custom_str5",
        "email_confirmation", "confirmation_address", "payment_method",
        "subscription_type", "billing_date", "recurring_amount", "frequency", "cycles"
    ];

    public static string Generate(IDictionary<string, string> parameters, string passphrase)
    {
        var parts = new List<string>();
        foreach (var key in CanonicalFieldOrder)
        {
            if (parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={WebUtility.UrlEncode(value.Trim())}");
            }
        }
        parts.Add($"passphrase={WebUtility.UrlEncode(passphrase.Trim())}");

        var queryString = string.Join("&", parts);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    ///     Verifies an inbound ITN signature. PayFast computes the ITN signature over the form fields
    ///     in the order they appear in the request body (not alphabetical, not the onsite canonical
    ///     order). The signature field itself is excluded. PHP empty/trim semantics apply.
    /// </summary>
    public static string GenerateForItn(IEnumerable<KeyValuePair<string, string>> orderedFields, string passphrase)
    {
        var parts = new List<string>();
        foreach (var (key, value) in orderedFields)
        {
            if (key == "signature") continue;
            if (string.IsNullOrWhiteSpace(value)) continue;
            parts.Add($"{key}={WebUtility.UrlEncode(value.Trim())}");
        }
        parts.Add($"passphrase={WebUtility.UrlEncode(passphrase.Trim())}");

        var queryString = string.Join("&", parts);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GenerateApiSignature(string merchantId, string passphrase, string timestamp)
    {
        var parameters = new SortedDictionary<string, string>
        {
            { "merchant-id", merchantId },
            { "passphrase", passphrase },
            { "timestamp", timestamp }
        };
        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={WebUtility.UrlEncode(p.Value)}"));
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
