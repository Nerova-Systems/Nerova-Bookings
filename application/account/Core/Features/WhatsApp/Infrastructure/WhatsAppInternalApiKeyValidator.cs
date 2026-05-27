using Microsoft.Extensions.Configuration;

namespace Account.Features.WhatsApp.Infrastructure;

public sealed class WhatsAppInternalApiKeyValidator(IConfiguration configuration) : IWhatsAppInternalApiKeyValidator
{
    public bool IsValid(string? authorizationHeader)
    {
        var expected = configuration["WhatsApp:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        if (authorizationHeader is null) return false;
        if (!authorizationHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)) return false;
        return string.Equals(authorizationHeader["ApiKey ".Length..].Trim(), expected, StringComparison.Ordinal);
    }
}
