using JetBrains.Annotations;

namespace Account.Features.AttributeSync.Domain;

/// <summary>
///     Identifies the identity-provider pathway that triggered an attribute sync operation.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncSource
{
    /// <summary>Microsoft Entra ID / Azure AD SSO login callback.</summary>
    MicrosoftSso,

    /// <summary>Google Workspace SSO login callback.</summary>
    GoogleSso,

    /// <summary>SCIM provisioning push (future).</summary>
    Scim,

    /// <summary>Admin-triggered manual sync via the management API.</summary>
    AdminManual
}
