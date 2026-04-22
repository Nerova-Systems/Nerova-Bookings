using System.Security.Cryptography;
using System.Text;

namespace Account.Integrations.PayFast;

public static class PayFastSignature
{
    public static string Generate(SortedDictionary<string, string> parameters, string passphrase)
    {
        var parts = parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}").ToList();
        parts.Add($"passphrase={Uri.EscapeDataString(passphrase)}");
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
        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
