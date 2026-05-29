using Main.Features.Apps.Domain;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.WhatsApp;

/// <summary>
///     Installer for WhatsApp Business. Unlike calendar and conferencing connectors,
///     WhatsApp Business does NOT run its own server-side OAuth flow: it is configured
///     client-side via Meta's Embedded Signup flow.
///     <para>
///         <see cref="BeginInstallAsync" /> returns a stub redirect back to our callback so
///         the platform's install→callback contract is exercised end-to-end.
///         <see cref="CompleteInstallAsync" /> returns a result with
///         <see cref="AppInstallCallbackResult.PersistCredential" /> set to <c>false</c>,
///         signalling the platform handler to create the <see cref="AppInstallation" /> row but
///         skip writing a duplicate <see cref="Credential" />.
///     </para>
/// </summary>
public sealed class WhatsAppInstaller : IAppInstaller
{
    public AppSlug Slug => new("whatsapp");

    public Task<AppInstallStartResult> BeginInstallAsync(AppInstallContext context, CancellationToken cancellationToken)
    {
        // Stub redirect to our own callback. The platform contract requires an authorize URL,
        // but WhatsApp has no server-side OAuth flow — the caller redirects to /api/apps/whatsapp/
        // /callback which lands in CompleteInstallAsync, where we signal "AppInstallation only, no Credential".
        var redirectUrl = $"{context.RedirectUri}?state={Uri.EscapeDataString(context.State)}&code=whatsapp-connected";
        return Task.FromResult(new AppInstallStartResult(redirectUrl, context.State));
    }

    public Task<AppInstallCallbackResult> CompleteInstallAsync(
        AppInstallCallbackContext context,
        CancellationToken cancellationToken
    )
    {
        // No fresh credential to persist: WhatsApp reuses client-side Embedded Signup and WabaConfiguration.
        // The platform's CompleteAppInstallHandler reads PersistCredential=false and creates
        // only the AppInstallation row.
        return Task.FromResult(new AppInstallCallbackResult(string.Empty, false));
    }

    public Task UninstallAsync(TenantId tenantId, UserId userId, string encryptedKey, CancellationToken cancellationToken)
    {
        // No-op: there is no whatsapp Credential to remove (we never created one).
        // The platform handler removes the AppInstallation row.
        _ = tenantId;
        _ = userId;
        _ = encryptedKey;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
