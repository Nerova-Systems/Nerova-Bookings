using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using Main.Features.Connectors.Domain;
using Main.Features.Connectors.Shared;
using Microsoft.Extensions.Hosting;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Connectors.Commands;

[PublicAPI]
public sealed record EnsureTestCoreConnectorCredentialsCommand(DateTimeOffset BusyStartTime, DateTimeOffset BusyEndTime)
    : ICommand, IRequest<Result<CoreConnectorAccountsResponse>>;

public sealed class EnsureTestCoreConnectorCredentialsHandler(
    IConnectorCredentialRepository connectorCredentialRepository,
    IExecutionContext executionContext,
    IHostEnvironment hostEnvironment,
    CoreConnectorOAuthProviderRegistry providerRegistry
) : IRequestHandler<EnsureTestCoreConnectorCredentialsCommand, Result<CoreConnectorAccountsResponse>>
{
    public async Task<Result<CoreConnectorAccountsResponse>> Handle(EnsureTestCoreConnectorCredentialsCommand command, CancellationToken cancellationToken)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return Result<CoreConnectorAccountsResponse>.NotFound("Core connector test fixtures are only available in development.");
        }

        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Result<CoreConnectorAccountsResponse>.From(authorization);
        if (command.BusyEndTime <= command.BusyStartTime)
        {
            return Result<CoreConnectorAccountsResponse>.BadRequest("Busy end time must be after busy start time.");
        }

        var tenantId = executionContext.TenantId!;
        var ownerUserId = executionContext.UserInfo.Id!;
        var ownerScope = OwnerScopeHash(tenantId, ownerUserId);
        var googleCredentialId = $"fake-busy:{ownerScope}|{command.BusyStartTime:O}/{command.BusyEndTime:O}";
        var officeCredentialId = $"e2e-office365-calendar:{ownerScope}:{command.BusyStartTime:O}/{command.BusyEndTime:O}";
        var zoomCredentialId = $"e2e-zoom-video:{ownerScope}:{command.BusyStartTime:O}/{command.BusyEndTime:O}";
        await connectorCredentialRepository.RemoveTestFixturesForOwnerAsync(tenantId, ownerUserId, [googleCredentialId, officeCredentialId, zoomCredentialId], cancellationToken);
        var googleCredential = await EnsureCredentialAsync(
            tenantId,
            ownerUserId,
            googleCredentialId,
            CoreConnectorConstants.GoogleCalendar,
            "google-test-account",
            "owner.google@example.test",
            "Owner Google Calendar",
            [
                new CoreConnectorCalendar("primary", "Primary calendar", true),
                new CoreConnectorCalendar("focus", "Focus calendar", false)
            ],
            cancellationToken
        );
        var officeCredential = await EnsureCredentialAsync(
            tenantId,
            ownerUserId,
            officeCredentialId,
            CoreConnectorConstants.Office365Calendar,
            "office-test-account",
            "owner.office@example.test",
            "Owner Office 365",
            [
                new CoreConnectorCalendar("calendar", "Calendar", true),
                new CoreConnectorCalendar("team", "Team calendar", false)
            ],
            cancellationToken
        );
        var zoomCredential = await EnsureCredentialAsync(
            tenantId,
            ownerUserId,
            zoomCredentialId,
            CoreConnectorConstants.ZoomVideo,
            "zoom-test-account",
            "owner.zoom@example.test",
            "Owner Zoom",
            [],
            cancellationToken
        );

        var credentials = new[] { googleCredential, officeCredential, zoomCredential };
        return new CoreConnectorAccountsResponse(
            credentials.Select(CoreConnectorAccountResponse.From).ToArray(),
            CoreConnectorConstants.CoreIntegrations
                .Select(integration => new CoreConnectorIntegrationResponse(
                        integration,
                        CoreConnectorConstants.Label(integration),
                        providerRegistry.GetProvider(integration)?.IsConfigured() == true,
                        credentials.Any(credential => credential.Integration.Equals(integration, StringComparison.OrdinalIgnoreCase))
                    )
                )
                .ToArray()
        );
    }

    private async Task<ConnectorCredential> EnsureCredentialAsync(
        TenantId tenantId,
        UserId ownerUserId,
        string id,
        string integration,
        string externalAccountId,
        string accountEmail,
        string displayName,
        CoreConnectorCalendar[] calendars,
        CancellationToken cancellationToken
    )
    {
        if (await connectorCredentialRepository.GetOwnedAsync(tenantId, ownerUserId, id, cancellationToken) is { } existingCredential) return existingCredential;

        var credential = ConnectorCredential.Create(
            tenantId,
            id,
            ownerUserId,
            integration,
            externalAccountId,
            accountEmail,
            displayName,
            "connected",
            $"secret://connectors/test/{id}",
            calendars
        );
        await connectorCredentialRepository.AddAsync(credential, cancellationToken);
        return credential;
    }

    private static string OwnerScopeHash(TenantId tenantId, UserId ownerUserId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId.Value}:{ownerUserId.Value}"));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
