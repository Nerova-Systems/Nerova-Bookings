using SharedKernel.Domain;

namespace Main.Features.Apps.Domain;

/// <summary>
///     Context passed to <see cref="IAppInstaller.BeginInstallAsync" /> when starting an OAuth flow.
///     Carries the redirect URI the connector should use plus the authenticated caller — connectors
///     use this to scope the authorize URL (e.g. set the <c>login_hint</c> for the user's email).
/// </summary>
public sealed record AppInstallContext(
    TenantId TenantId,
    UserId UserId,
    string UserEmail,
    string RedirectUri,
    string State
);

/// <summary>
///     Result of starting an OAuth install flow.
/// </summary>
public sealed record AppInstallStartResult(string AuthorizeUrl, string State);

/// <summary>
///     Context for completing an OAuth flow (the redirect-back step).
/// </summary>
public sealed record AppInstallCallbackContext(
    TenantId TenantId,
    UserId UserId,
    string Code,
    string RedirectUri
);

/// <summary>
///     Result of completing the OAuth flow. The <paramref name="EncryptedKey" /> field is the
///     already-encrypted JSON blob the platform will persist in <see cref="Credential" />.
///     Connectors call <c>CredentialProtector</c> to produce it.
///     <para>
///         Connectors that piggy-back on another connector's credential (e.g. Google Meet
///         reuses the Google Calendar tokens) set <paramref name="PersistCredential" /> to
///         <c>false</c> so the platform skips writing a duplicate <see cref="Credential" />
///         row. In that case <paramref name="EncryptedKey" /> may be empty.
///     </para>
/// </summary>
public sealed record AppInstallCallbackResult(string EncryptedKey, bool PersistCredential = true);

/// <summary>
///     The contract every connector implements (one implementation per <see cref="AppSlug" />) to
///     drive the OAuth install / uninstall lifecycle. Connector tracks (T2-*) register
///     implementations against <see cref="IAppRegistry" />; this foundation track ships the
///     contract and an empty registry.
/// </summary>
public interface IAppInstaller
{
    /// <summary>The slug this installer handles. Must match a registered <see cref="App" />.</summary>
    AppSlug Slug { get; }

    /// <summary>
    ///     Build the provider authorize URL the caller should be redirected to. Receives the
    ///     opaque <see cref="AppInstallContext.State" /> token to embed in the redirect for CSRF
    ///     protection — implementations must include it verbatim in the URL.
    /// </summary>
    Task<AppInstallStartResult> BeginInstallAsync(AppInstallContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Exchange the OAuth <see cref="AppInstallCallbackContext.Code" /> for tokens and return
    ///     them as an encrypted JSON blob ready to persist as a <see cref="Credential" />.
    /// </summary>
    Task<AppInstallCallbackResult> CompleteInstallAsync(AppInstallCallbackContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Optional cleanup hook called before credentials are deleted. Connectors may revoke
    ///     tokens at the provider here. Default implementation is a no-op so connectors only
    ///     override when they need to.
    /// </summary>
    Task UninstallAsync(TenantId tenantId, UserId userId, string encryptedKey, CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = userId;
        _ = encryptedKey;
        return Task.CompletedTask;
    }
}
