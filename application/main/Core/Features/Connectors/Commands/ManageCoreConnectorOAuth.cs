using System.Text.Json;
using System.Security.Cryptography;
using Main.Database;
using Main.Features.Connectors.Domain;
using Main.Features.Connectors.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Connectors.Commands;

public sealed record GetCoreConnectorAuthorizationUrlQuery(string Integration, string? ReturnTo)
    : IRequest<Result<CoreConnectorAuthorizationUrlResponse>>;

public sealed class GetCoreConnectorAuthorizationUrlHandler(
    CoreConnectorOAuthProviderRegistry providerRegistry,
    IDataProtectionProvider dataProtectionProvider,
    IExecutionContext executionContext,
    IConfiguration configuration,
    TimeProvider timeProvider
) : IRequestHandler<GetCoreConnectorAuthorizationUrlQuery, Result<CoreConnectorAuthorizationUrlResponse>>
{
    private readonly IDataProtector _stateProtector = dataProtectionProvider.CreateProtector("Main.CoreConnectors.OAuthState");

    public Task<Result<CoreConnectorAuthorizationUrlResponse>> Handle(GetCoreConnectorAuthorizationUrlQuery query, CancellationToken cancellationToken)
    {
        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Task.FromResult(Result<CoreConnectorAuthorizationUrlResponse>.From(authorization));

        var integration = query.Integration.Trim();
        var provider = providerRegistry.GetProvider(integration);
        if (provider is null)
        {
            return Task.FromResult(Result<CoreConnectorAuthorizationUrlResponse>.BadRequest($"Core connector integration '{integration}' is not supported."));
        }

        if (!provider.IsConfigured())
        {
            return Task.FromResult(Result<CoreConnectorAuthorizationUrlResponse>.BadRequest($"Core connector integration '{integration}' is not configured."));
        }

        var returnTo = SafeReturnTo(query.ReturnTo);
        var state = new CoreConnectorOAuthState(
            executionContext.TenantId!,
            executionContext.UserInfo.Id!,
            integration,
            returnTo,
            Guid.NewGuid().ToString("N"),
            timeProvider.GetUtcNow().AddMinutes(10)
        );
        var protectedState = _stateProtector.Protect(JsonSerializer.Serialize(state));
        var authorizationUrl = provider.BuildAuthorizationUrl(BuildCallbackUrl(integration), protectedState);
        return Task.FromResult(Result<CoreConnectorAuthorizationUrlResponse>.Success(new CoreConnectorAuthorizationUrlResponse(authorizationUrl)));
    }

    private string BuildCallbackUrl(string integration)
    {
        var publicUrl = (configuration["Connectors:Core:OAuth:PublicUrl"] ?? configuration["PublicUrl"] ?? "https://localhost").TrimEnd('/');
        return $"{publicUrl}/api/connectors/core/{Uri.EscapeDataString(integration)}/callback";
    }

    private static string SafeReturnTo(string? returnTo)
    {
        if (string.IsNullOrWhiteSpace(returnTo)) return "/event-types";
        var trimmed = returnTo.Trim();
        if (!trimmed.StartsWith('/')) return "/event-types";
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return "/event-types";
        return trimmed;
    }
}

public sealed record CompleteCoreConnectorOAuthCallbackCommand(string Integration, string? Code, string? State)
    : ICommand, IRequest<Result<string>>;

public sealed class CompleteCoreConnectorOAuthCallbackHandler(
    CoreConnectorOAuthProviderRegistry providerRegistry,
    IConnectorCredentialRepository connectorCredentialRepository,
    IConnectorTokenStore connectorTokenStore,
    IDataProtectionProvider dataProtectionProvider,
    IExecutionContext executionContext,
    IConfiguration configuration,
    TimeProvider timeProvider,
    MainDbContext mainDbContext
) : IRequestHandler<CompleteCoreConnectorOAuthCallbackCommand, Result<string>>
{
    private readonly IDataProtector _stateProtector = dataProtectionProvider.CreateProtector("Main.CoreConnectors.OAuthState");

    public async Task<Result<string>> Handle(CompleteCoreConnectorOAuthCallbackCommand command, CancellationToken cancellationToken)
    {
        var stateResult = UnprotectState(command.State);
        if (stateResult is null) return FailedRedirect("/event-types", "invalid_state");

        var returnTo = SafeReturnTo(stateResult.ReturnTo);
        if (stateResult.ExpiresAt < timeProvider.GetUtcNow() ||
            executionContext.TenantId != stateResult.TenantId ||
            executionContext.UserInfo.Id != stateResult.OwnerUserId ||
            !stateResult.Integration.Equals(command.Integration.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return FailedRedirect(returnTo, "invalid_state");
        }

        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Result<string>.From(authorization);
        if (string.IsNullOrWhiteSpace(command.Code)) return FailedRedirect(returnTo, "missing_code");

        var provider = providerRegistry.GetProvider(stateResult.Integration);
        if (provider is null || !provider.IsConfigured()) return FailedRedirect(returnTo, "provider_error");

        CoreConnectorOAuthCallbackResult callbackResult;
        try
        {
            callbackResult = await provider.CompleteCallbackAsync(command.Code.Trim(), BuildCallbackUrl(stateResult.Integration), cancellationToken);
        }
        catch (CoreConnectorOAuthException exception)
        {
            return FailedRedirect(returnTo, exception.Code);
        }
        catch (HttpRequestException)
        {
            return FailedRedirect(returnTo, "provider_error");
        }

        var tenantId = executionContext.TenantId!;
        var ownerUserId = executionContext.UserInfo.Id!;
        var credentialId = CoreConnectorCredentialIds.CredentialId(tenantId, ownerUserId, stateResult.Integration, callbackResult.Account.ExternalAccountId);
        var tokenSecretId = CoreConnectorCredentialIds.TokenSecretId(tenantId, ownerUserId, stateResult.Integration, callbackResult.Account.ExternalAccountId);
        var secretReference = $"{CoreConnectorCredentialIds.ProtectedTokenReferencePrefix}{tokenSecretId}";
        var credential = await connectorCredentialRepository.GetOwnedByExternalAccountAsync(
            tenantId,
            ownerUserId,
            stateResult.Integration,
            callbackResult.Account.ExternalAccountId,
            cancellationToken
        );

        if (credential is null)
        {
            credential = ConnectorCredential.Create(
                tenantId,
                credentialId,
                ownerUserId,
                stateResult.Integration,
                callbackResult.Account.ExternalAccountId,
                callbackResult.Account.AccountEmail,
                callbackResult.Account.DisplayName,
                "connected",
                secretReference,
                callbackResult.Account.Calendars
            );
            await connectorCredentialRepository.AddAsync(credential, cancellationToken);
        }
        else
        {
            credential.UpdateAccount(callbackResult.Account.AccountEmail, callbackResult.Account.DisplayName, "connected", secretReference, callbackResult.Account.Calendars);
            connectorCredentialRepository.Update(credential);
        }

        await connectorTokenStore.SaveAsync(tenantId, tokenSecretId, credential.Id, callbackResult.TokenSet, cancellationToken);
        await mainDbContext.SaveChangesAsync(cancellationToken);
        return Result<string>.Redirect(AppendQuery(returnTo, "connector", stateResult.Integration));
    }

    private CoreConnectorOAuthState? UnprotectState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        try
        {
            return JsonSerializer.Deserialize<CoreConnectorOAuthState>(_stateProtector.Unprotect(state));
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or CryptographicException)
        {
            return null;
        }
    }

    private string BuildCallbackUrl(string integration)
    {
        var publicUrl = (configuration["Connectors:Core:OAuth:PublicUrl"] ?? configuration["PublicUrl"] ?? "https://localhost").TrimEnd('/');
        return $"{publicUrl}/api/connectors/core/{Uri.EscapeDataString(integration)}/callback";
    }

    private static Result<string> FailedRedirect(string returnTo, string error)
    {
        return Result<string>.Redirect(AppendQuery(SafeReturnTo(returnTo), "error", error));
    }

    private static string SafeReturnTo(string? returnTo)
    {
        if (string.IsNullOrWhiteSpace(returnTo)) return "/event-types";
        var trimmed = returnTo.Trim();
        if (!trimmed.StartsWith('/')) return "/event-types";
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return "/event-types";
        return trimmed;
    }

    private static string AppendQuery(string path, string key, string value)
    {
        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}

public sealed record DeleteCoreConnectorAccountCommand(string CredentialId) : ICommand, IRequest<Result>;

public sealed class DeleteCoreConnectorAccountHandler(
    IConnectorCredentialRepository connectorCredentialRepository,
    IConnectorTokenStore connectorTokenStore,
    IExecutionContext executionContext,
    MainDbContext mainDbContext
) : IRequestHandler<DeleteCoreConnectorAccountCommand, Result>
{
    public async Task<Result> Handle(DeleteCoreConnectorAccountCommand command, CancellationToken cancellationToken)
    {
        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Result.From(authorization);

        var tenantId = executionContext.TenantId!;
        var ownerUserId = executionContext.UserInfo.Id!;
        var credential = await connectorCredentialRepository.GetOwnedAsync(tenantId, ownerUserId, command.CredentialId, cancellationToken);
        if (credential is null)
        {
            return Result.NotFound($"Connector credential '{command.CredentialId}' was not found.");
        }

        connectorCredentialRepository.Remove(credential);
        await connectorTokenStore.RemoveForCredentialAsync(tenantId, credential.Id, cancellationToken);
        await mainDbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
