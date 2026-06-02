namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     OAuth client credentials and endpoint configuration for the Zoom conferencing
///     connector. Bound from environment variables at startup. In development the values may
///     be empty — the installer surfaces a "not configured" exception so the rest of the app
///     still boots.
/// </summary>
public sealed class ZoomOptions
{
    /// <summary>
    ///     The Zoom Marketplace scope required to create and delete meetings via the
    ///     <c>https://api.zoom.us/v2/users/{userId}/meetings</c> endpoints. Zoom does not accept
    ///     a <c>scope</c> parameter on the authorize call — granted scopes are configured on the
    ///     Zoom Marketplace app itself — so this constant documents the real scope the Marketplace
    ///     app must be granted for the connector to function. It is surfaced through the Apps API
    ///     so users see the exact permission the integration relies on.
    /// </summary>
    public const string MeetingWriteScope = "meeting:write";

    /// <summary>OAuth 2.0 client id (from Zoom Marketplace → Your App → Credentials).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>OAuth authorize endpoint.</summary>
    public string AuthorizeUrl { get; init; } = "https://zoom.us/oauth/authorize";

    /// <summary>OAuth token endpoint (code exchange + refresh). Requires HTTP Basic auth with the client credentials.</summary>
    public string TokenUrl { get; init; } = "https://zoom.us/oauth/token";

    /// <summary>OAuth token revocation endpoint.</summary>
    public string RevokeUrl { get; init; } = "https://zoom.us/oauth/revoke";

    /// <summary>Zoom REST API base URL.</summary>
    public string ApiBaseUrl { get; init; } = "https://api.zoom.us/v2";
}
