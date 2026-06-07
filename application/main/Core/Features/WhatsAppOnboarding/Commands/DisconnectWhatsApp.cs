using JetBrains.Annotations;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.WhatsAppOnboarding.Commands;

[PublicAPI]
public sealed record DisconnectWhatsAppCommand : ICommand, IRequest<Result>;

public sealed class DisconnectWhatsAppHandler(
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DisconnectWhatsAppCommand, Result>
{
    public async Task<Result> Handle(DisconnectWhatsAppCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != "Owner")
        {
            return Result.Forbidden("Only owners can disconnect a WhatsApp Business Account.");
        }

        var account = await whatsAppBusinessAccountRepository.GetByTenantAsync(cancellationToken);
        if (account is null)
        {
            return Result.Success();
        }

        whatsAppBusinessAccountRepository.Remove(account);
        events.CollectEvent(new WhatsAppBusinessAccountDisconnected(account.Id));

        return Result.Success();
    }
}
