using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppFlows.Queries;

[PublicAPI]
public sealed record GetTenantFlowConfigQuery : IRequest<Result<TenantFlowConfigResponse>>;

public sealed class GetTenantFlowConfigHandler(
    ITenantFlowConfigRepository repository,
    IWhatsAppFlowProfileSync profileSync,
    IExecutionContext executionContext
) : IRequestHandler<GetTenantFlowConfigQuery, Result<TenantFlowConfigResponse>>
{
    public async Task<Result<TenantFlowConfigResponse>> Handle(GetTenantFlowConfigQuery request, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<TenantFlowConfigResponse>.Unauthorized("Authentication is required.");

        var config = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (config is null) return Result<TenantFlowConfigResponse>.NotFound("Tenant flow configuration has not been created yet.");

        // The display phone is owned by the account SCS; the sync is best-effort here — the
        // questionnaire UI still functions if the cross-SCS lookup fails, the link page just
        // won't have a real number to show.
        var profile = await profileSync.GetByTenantId(tenantId, cancellationToken);
        return TenantFlowConfigResponse.From(config, profile?.DisplayPhoneNumber);
    }
}

