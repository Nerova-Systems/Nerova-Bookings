using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.MsTeams;

/// <summary>
///     Installer for Microsoft Teams. Like Google Meet, MS Teams does NOT run its own OAuth
///     flow: meeting links are generated via Microsoft Graph's <c>/me/onlineMeetings</c>
///     endpoint, reusing the host's existing <c>office365-calendar</c>
///     <see cref="Credential" />. The Office 365 Calendar connector requests
///     <c>OnlineMeetings.ReadWrite</c> up front (see <see cref="Office365CalendarOptions" />)
///     so a fresh install of Calendar grants Teams in the same consent screen.
///     <para>
///         <see cref="BeginInstallAsync" /> and <see cref="CompleteInstallAsync" /> both verify
///         two prerequisites: the user has an Office 365 Calendar credential, and that
///         credential's stored <c>scope</c> includes <c>OnlineMeetings.ReadWrite</c>. Users
///         who installed Calendar before this scope was requested must reconnect Calendar
///         before Teams can be installed — surfaced as
///         <see cref="MsTeamsPrerequisiteMissingException" /> (412 once the platform handler
///         grows that mapping).
///     </para>
///     <para>
///         <see cref="CompleteInstallAsync" /> returns
///         <see cref="AppInstallCallbackResult.PersistCredential" /> = <c>false</c> so the
///         platform handler creates the <see cref="AppInstallation" /> row but skips writing
///         a duplicate <see cref="Credential" />. Mirrors cal.com's <c>office365video</c> app.
///     </para>
///     <para>
///         Registered as a singleton (matches the registry lifetime); request-scoped
///         dependencies (<see cref="ICredentialRepository" />, <see cref="CredentialProtector" />)
///         are resolved per call via a fresh <see cref="IServiceScope" />.
///     </para>
/// </summary>
public sealed class MsTeamsInstaller(IServiceScopeFactory scopeFactory) : IAppInstaller
{
    public AppSlug Slug => MsTeamsSlug.Slug;

    public IReadOnlyList<AppPermission> Permissions => MsTeamsPermissions.All;

    public async Task<AppInstallStartResult> BeginInstallAsync(AppInstallContext context, CancellationToken cancellationToken)
    {
        await EnsureOffice365PrerequisiteAsync(context.UserId, cancellationToken);

        // Stub redirect to our own callback — no real OAuth, matches the Google Meet pattern.
        var redirectUrl = $"{context.RedirectUri}?state={Uri.EscapeDataString(context.State)}&code=reuse-office365-calendar";
        return new AppInstallStartResult(redirectUrl, context.State);
    }

    public async Task<AppInstallCallbackResult> CompleteInstallAsync(
        AppInstallCallbackContext context,
        CancellationToken cancellationToken
    )
    {
        await EnsureOffice365PrerequisiteAsync(context.UserId, cancellationToken);

        // No fresh credential to persist — MS Teams reuses the office365-calendar credential.
        return new AppInstallCallbackResult(string.Empty, false);
    }

    public Task UninstallAsync(TenantId tenantId, UserId userId, string encryptedKey, CancellationToken cancellationToken)
    {
        // No-op: there is no ms-teams Credential to remove (we never created one), and we
        // must NOT touch the office365-calendar credential because Calendar may still be in
        // use independently. The platform handler removes the AppInstallation row.
        _ = tenantId;
        _ = userId;
        _ = encryptedKey;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task EnsureOffice365PrerequisiteAsync(UserId userId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var credentialRepository = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
        var credential = await credentialRepository.GetForUserAsync(userId, Office365CalendarSlug.Slug, cancellationToken);
        if (credential is null) throw new MsTeamsPrerequisiteMissingException();

        // Verify the existing credential was granted the OnlineMeetings.ReadWrite scope. The
        // Office 365 connector requests it up front today, but a user who installed Calendar
        // before that change will have a credential blob with only Calendars.ReadWrite. We
        // can't silently upgrade the consent — Microsoft requires a fresh authorize round
        // trip — so we surface a clean "reconnect Calendar" error instead.
        var protector = scope.ServiceProvider.GetRequiredService<CredentialProtector>();
        Office365CredentialBlob blob;
        try
        {
            blob = Office365CredentialBlob.FromJson(protector.Unprotect(credential.EncryptedKey));
        }
        catch
        {
            // Corrupted blob — treat as missing prerequisite. The user reconnects Calendar
            // and we are back in a known good state.
            throw new MsTeamsPrerequisiteMissingException();
        }

        if (!HasOnlineMeetingsScope(blob.Scope))
        {
            throw new MsTeamsPrerequisiteMissingException(
                "Office 365 Calendar must be reconnected to grant the Microsoft Teams 'OnlineMeetings.ReadWrite' scope before Teams can be installed."
            );
        }
    }

    /// <summary>
    ///     Microsoft returns scopes as a space-separated, case-insensitive string. Defensive
    ///     against leading/trailing whitespace and the (rare) tenant that lowercases the
    ///     scope names. Public so the installer tests can pin the matching semantics directly
    ///     — the rule is subtle enough that an exported test boundary is worth the surface.
    /// </summary>
    public static bool HasOnlineMeetingsScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return false;
        var tokens = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (string.Equals(token, Office365CalendarOptions.OnlineMeetingsScope, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     Thrown when a user attempts to install Microsoft Teams without the Office 365 Calendar
///     prerequisite — either the calendar is not installed at all, or it was installed before
///     the <c>OnlineMeetings.ReadWrite</c> scope was added and must be reconnected. Surfaces
///     as a 412 Precondition Failed at the API boundary once the platform install handler
///     grows explicit prerequisite mapping (deferral mirrors
///     <see cref="GoogleMeet.GoogleMeetPrerequisiteMissingException" />).
/// </summary>
public sealed class MsTeamsPrerequisiteMissingException(string? message = null)
    : InvalidOperationException(message ?? "Microsoft Office 365 Calendar must be installed before Microsoft Teams — Teams reuses the Office 365 credential.");
