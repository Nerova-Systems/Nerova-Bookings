using System.Text.RegularExpressions;
using SharedKernel.Authentication;

namespace Main.Features.Scheduling.Shared;

public static partial class SchedulingProfileHandle
{
    private static readonly HashSet<string> ReservedHandles = new(StringComparer.OrdinalIgnoreCase)
    {
        "account",
        "api",
        "availability",
        "bookings",
        "components",
        "dashboard",
        "error",
        "event-types",
        "legal",
        "login",
        "profile",
        "signup",
        "user",
        "welcome"
    };

    public static bool IsReserved(string handle)
    {
        return ReservedHandles.Contains(handle);
    }

    public static string DefaultFrom(UserInfo userInfo)
    {
        var emailLocalPart = userInfo.Email?.Split('@')[0] ?? string.Empty;
        var fullName = $"{userInfo.FirstName} {userInfo.LastName}".Trim();
        var source = string.IsNullOrWhiteSpace(emailLocalPart) ? fullName : emailLocalPart;
        var normalized = Normalize(source);
        return string.IsNullOrWhiteSpace(normalized) ? $"user-{userInfo.Id}" : normalized;
    }

    public static string Normalize(string value)
    {
        var normalized = HandleCharacters().Replace(value.Trim().ToLowerInvariant(), "-");
        return normalized.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HandleCharacters();
}
