using JetBrains.Annotations;
using Main.Features.Connectors.Domain;
using Main.Features.Connectors.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Connectors.Queries;

[PublicAPI]
public sealed record GetCoreConnectorAccountsQuery : IRequest<Result<CoreConnectorAccountsResponse>>;

public sealed class GetCoreConnectorAccountsHandler(
    IConnectorCredentialRepository connectorCredentialRepository,
    IExecutionContext executionContext,
    CoreConnectorOAuthProviderRegistry providerRegistry
) : IRequestHandler<GetCoreConnectorAccountsQuery, Result<CoreConnectorAccountsResponse>>
{
    public async Task<Result<CoreConnectorAccountsResponse>> Handle(GetCoreConnectorAccountsQuery query, CancellationToken cancellationToken)
    {
        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Result<CoreConnectorAccountsResponse>.From(authorization);

        var credentials = await connectorCredentialRepository.GetCoreForOwnerAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, cancellationToken);
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
}
