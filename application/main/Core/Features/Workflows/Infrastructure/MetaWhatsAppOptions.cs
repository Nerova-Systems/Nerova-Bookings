namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Meta WhatsApp Business Cloud API credentials. Bound from environment variables at startup.
///     When values are missing, <see cref="MetaWhatsAppProvider" /> short-circuits and returns
///     <c>NotConfigured</c> instead of throwing so the workflow scheduler keeps ticking.
/// </summary>
public sealed class MetaWhatsAppOptions
{
    /// <summary>WhatsApp Business phone-number ID — appears in the Meta App dashboard.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>System-user access token (Bearer) with <c>whatsapp_business_messaging</c> scope.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Graph API base URL — overridable for tests.</summary>
    public string ApiBaseUrl { get; set; } = "https://graph.facebook.com/v18.0";

    /// <summary>Language tag for the approved template (e.g. "en", "en_US").</summary>
    public string DefaultLanguageCode { get; set; } = "en";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PhoneNumberId) && !string.IsNullOrWhiteSpace(AccessToken);
}
