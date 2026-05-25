namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     OAuth client credentials and endpoint configuration for the Microsoft Office 365
///     (Outlook) Calendar connector. Bound from environment variables at startup. In
///     development the values may be empty — the installer surfaces a "not configured"
///     exception so the rest of the app still boots.
///     <para>
///         The Microsoft identity platform v2.0 endpoints use the <c>common</c> tenant by
///         default so the installer works for both personal Microsoft accounts and any work /
///         school tenant; override <see cref="TenantId" /> if the deployment is locked to a
///         single tenant.
///     </para>
/// </summary>
public sealed class Office365CalendarOptions
{
    /// <summary>OAuth 2.0 application (client) id (from Microsoft Entra ID → App registrations).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret (from Microsoft Entra ID → App registrations).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>Entra ID tenant segment to use in the OAuth URLs. <c>common</c> = multi-tenant + personal.</summary>
    public string TenantId { get; set; } = "common";

    /// <summary>OAuth authorize endpoint (composed against <see cref="TenantId" /> at request time).</summary>
    public string AuthorizeUrl => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize";

    /// <summary>OAuth token endpoint (code exchange + refresh).</summary>
    public string TokenUrl => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";

    /// <summary>Microsoft Graph API base URL.</summary>
    public string ApiBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

    /// <summary>
    ///     Scopes requested at authorize time. <c>offline_access</c> is required for refresh
    ///     tokens; <c>Calendars.ReadWrite</c> covers both free-busy lookups (via getSchedule)
    ///     and event create/update/cancel.
    /// </summary>
    public string[] Scopes { get; set; } =
    [
        "offline_access",
        "Calendars.ReadWrite"
    ];
}
