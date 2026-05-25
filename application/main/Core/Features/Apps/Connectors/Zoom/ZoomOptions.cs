namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     OAuth client credentials and endpoint configuration for the Zoom conferencing
///     connector. Bound from environment variables at startup. In development the values may
///     be empty — the installer surfaces a "not configured" exception so the rest of the app
///     still boots.
/// </summary>
public sealed class ZoomOptions
{
    /// <summary>OAuth 2.0 client id (from Zoom Marketplace → Your App → Credentials).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>OAuth authorize endpoint.</summary>
    public string AuthorizeUrl { get; set; } = "https://zoom.us/oauth/authorize";

    /// <summary>OAuth token endpoint (code exchange + refresh). Requires HTTP Basic auth with the client credentials.</summary>
    public string TokenUrl { get; set; } = "https://zoom.us/oauth/token";

    /// <summary>OAuth token revocation endpoint.</summary>
    public string RevokeUrl { get; set; } = "https://zoom.us/oauth/revoke";

    /// <summary>Zoom REST API base URL.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.zoom.us/v2";
}
