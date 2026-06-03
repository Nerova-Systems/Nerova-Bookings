namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Global (non-tenant) configuration for the Meta WhatsApp Business Cloud API adapter.
///     Per-tenant phone-number IDs and access tokens are stored in the database inside
///     <c>WhatsAppBusinessAccount</c> and are never placed in server configuration.
/// </summary>
public sealed class MetaWhatsAppOptions
{
    /// <summary>Graph API base URL — overridable for tests.</summary>
    public string ApiBaseUrl { get; init; } = "https://graph.facebook.com/v18.0";

    /// <summary>Language tag for the approved template (e.g. "en", "en_US").</summary>
    public string DefaultLanguageCode { get; init; } = "en";
}
