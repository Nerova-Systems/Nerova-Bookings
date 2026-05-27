using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Commands;

[PublicAPI]
public sealed record UpdateFlowStatusInternalCommand(
    TenantId TenantId,
    string FlowId,
    string Status,
    string? GeneratedFlowJson
) : ICommand, IRequest<Result>;

public sealed class UpdateFlowStatusInternalHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<UpdateFlowStatusInternalCommand, Result>
{
    public async Task<Result> Handle(UpdateFlowStatusInternalCommand command, CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(command.TenantId, cancellationToken);
        if (config is null) return Result.NotFound("WABA configuration not found for tenant.");

        config.SetFlowId(command.FlowId);
        if (Enum.TryParse<WabaFlowStatus>(command.Status, ignoreCase: true, out var parsed))
        {
            config.SetFlowStatus(parsed);
        }
        if (!string.IsNullOrWhiteSpace(command.GeneratedFlowJson))
        {
            config.SetGeneratedFlowJson(command.GeneratedFlowJson);
        }

        repository.Update(config);
        return Result.Success();
    }
}
