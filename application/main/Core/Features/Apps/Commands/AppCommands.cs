using JetBrains.Annotations;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Shared;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.SinglePageApp;
using SharedKernel.Telemetry;

namespace Main.Features.Apps.Commands;

// ─── Install ─────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.App, PermissionAction.Create)]
public sealed record InstallAppCommand(AppSlug Slug) : ICommand, IRequest<Result<InstallAppResponse>>;

public sealed class InstallAppHandler(
    IAppRepository appRepository,
    IAppRegistry appRegistry,
    IOAuthStateStore stateStore,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<InstallAppCommand, Result<InstallAppResponse>>
{
    public async Task<Result<InstallAppResponse>> Handle(InstallAppCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        var userId = executionContext.UserInfo.Id;
        var userEmail = executionContext.UserInfo.Email;
        if (tenantId is null || userId is null || userEmail is null)
        {
            return Result<InstallAppResponse>.Unauthorized("Authentication is required.");
        }

        var app = await appRepository.GetByIdAsync(command.Slug, cancellationToken);
        if (app is null) return Result<InstallAppResponse>.NotFound($"App '{command.Slug}' was not found.");
        if (!app.IsActive) return Result<InstallAppResponse>.BadRequest($"App '{command.Slug}' is not active.");

        var state = stateStore.Issue(new OAuthStateEntry(tenantId, userId, command.Slug));
        var redirectUri = AppCallbackUrlBuilder.Build(command.Slug);

        var installer = appRegistry.Resolve(command.Slug);
        if (installer is null)
        {
            // Foundation track: no connector installer registered. Return a stub authorize URL so
            // the endpoint contract is exercisable end-to-end. T2 tracks register real installers.
            events.CollectEvent(new AppInstallStarted(command.Slug));
            return new InstallAppResponse($"https://example.invalid/oauth/authorize?state={state}", state);
        }

        var context = new AppInstallContext(tenantId, userId, userEmail, redirectUri, state);
        var result = await installer.BeginInstallAsync(context, cancellationToken);
        events.CollectEvent(new AppInstallStarted(command.Slug));
        return new InstallAppResponse(result.AuthorizeUrl, result.State);
    }
}

// ─── Callback ────────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.App, PermissionAction.Create)]
public sealed record CompleteAppInstallCommand(AppSlug Slug, string Code, string State)
    : ICommand, IRequest<Result<AppCallbackResponse>>;

public sealed class CompleteAppInstallHandler(
    IAppRepository appRepository,
    IAppRegistry appRegistry,
    IOAuthStateStore stateStore,
    ICredentialRepository credentialRepository,
    IAppInstallationRepository installationRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<CompleteAppInstallCommand, Result<AppCallbackResponse>>
{
    public async Task<Result<AppCallbackResponse>> Handle(CompleteAppInstallCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Code)) return Result<AppCallbackResponse>.BadRequest("Authorization code is required.");
        if (string.IsNullOrWhiteSpace(command.State)) return Result<AppCallbackResponse>.BadRequest("State token is required.");

        var entry = stateStore.Consume(command.State);
        if (entry is null) return Result<AppCallbackResponse>.BadRequest("OAuth state is invalid or expired.");
        if (entry.AppSlug != command.Slug) return Result<AppCallbackResponse>.BadRequest("OAuth state does not match the requested app.");

        var tenantId = executionContext.TenantId;
        var userId = executionContext.UserInfo.Id;
        if (tenantId is null || userId is null) return Result<AppCallbackResponse>.Unauthorized("Authentication is required.");
        if (entry.TenantId != tenantId || entry.UserId != userId)
        {
            return Result<AppCallbackResponse>.Forbidden("OAuth state belongs to a different session.");
        }

        var app = await appRepository.GetByIdAsync(command.Slug, cancellationToken);
        if (app is null) return Result<AppCallbackResponse>.NotFound($"App '{command.Slug}' was not found.");

        var installer = appRegistry.Resolve(command.Slug);
        string encryptedKey;
        var persistCredential = true;
        if (installer is null)
        {
            // Foundation track stub: persist a placeholder encrypted blob so the contract is observable.
            // T2 tracks replace this branch with the installer's real token exchange.
            encryptedKey = $"stub:{command.Slug}:{command.Code}";
        }
        else
        {
            var redirectUri = AppCallbackUrlBuilder.Build(command.Slug);
            var context = new AppInstallCallbackContext(tenantId, userId, command.Code, redirectUri);
            var result = await installer.CompleteInstallAsync(context, cancellationToken);
            encryptedKey = result.EncryptedKey;
            persistCredential = result.PersistCredential;
        }

        if (persistCredential)
        {
            var existingCredential = await credentialRepository.GetForUserAsync(userId, command.Slug, cancellationToken);
            if (existingCredential is null)
            {
                var credential = Credential.Create(tenantId, userId, command.Slug, encryptedKey);
                await credentialRepository.AddAsync(credential, cancellationToken);
            }
            else
            {
                existingCredential.UpdateKey(encryptedKey);
                credentialRepository.Update(existingCredential);
            }
        }

        var installation = await installationRepository.GetForTenantAsync(command.Slug, cancellationToken);
        if (installation is null)
        {
            installation = AppInstallation.Create(tenantId, command.Slug, userId, timeProvider.GetUtcNow());
            await installationRepository.AddAsync(installation, cancellationToken);
        }

        events.CollectEvent(new AppInstallCompleted(command.Slug));
        return new AppCallbackResponse(command.Slug, true);
    }
}

// ─── Uninstall ───────────────────────────────────────────────────────────

[PublicAPI]
[RequirePermission(PermissionResource.App, PermissionAction.Delete)]
public sealed record UninstallAppCommand(AppSlug Slug) : ICommand, IRequest<Result>;

public sealed class UninstallAppHandler(
    IAppRegistry appRegistry,
    ICredentialRepository credentialRepository,
    IAppInstallationRepository installationRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UninstallAppCommand, Result>
{
    public async Task<Result> Handle(UninstallAppCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        var userId = executionContext.UserInfo.Id;
        if (tenantId is null || userId is null) return Result.Unauthorized("Authentication is required.");

        var credential = await credentialRepository.GetForUserAsync(userId, command.Slug, cancellationToken);
        if (credential is not null)
        {
            var installer = appRegistry.Resolve(command.Slug);
            if (installer is not null)
            {
                await installer.UninstallAsync(tenantId, userId, credential.EncryptedKey, cancellationToken);
            }

            credentialRepository.Remove(credential);
        }

        // Tenant installation row is removed only when the last user disconnects. The simplest
        // observable rule: when no credentials remain for this slug after deletion, drop the
        // installation. For Wave 5 with single-tenant solo users this collapses to "always remove
        // when the user disconnects" — good enough for the foundation; T2 tracks may refine.
        // Note: the just-removed credential is excluded by Id because EF has only marked it for
        // deletion at this point — SaveChanges runs later in the unit-of-work pipeline.
        var remaining = await credentialRepository.GetForUserAsync(userId, cancellationToken);
        if (remaining.All(other => other.AppSlug != command.Slug || (credential is not null && other.Id == credential.Id)))
        {
            var installation = await installationRepository.GetForTenantAsync(command.Slug, cancellationToken);
            if (installation is not null) installationRepository.Remove(installation);
        }

        events.CollectEvent(new AppUninstalled(command.Slug));
        return Result.Success();
    }
}

internal static class AppCallbackUrlBuilder
{
    public static string Build(AppSlug slug)
    {
        var publicUrl = Environment.GetEnvironmentVariable("OAUTH_PUBLIC_URL")
                        ?? Environment.GetEnvironmentVariable(SinglePageAppConfiguration.PublicUrlKey)
                        ?? "https://localhost";
        return $"{publicUrl.TrimEnd('/')}/api/apps/{slug.Value}/callback";
    }
}
