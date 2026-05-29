using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Commands;

[PublicAPI]
public sealed record DisconnectWabaCommand(TenantId TenantId) : ICommand, IRequest<Result>;

public sealed class DisconnectWabaHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<DisconnectWabaCommand, Result>
{
    public async Task<Result> Handle(DisconnectWabaCommand command, CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(command.TenantId, cancellationToken);
        if (config is not null)
        {
            repository.Remove(config);
        }

        return Result.Success();
    }
}
