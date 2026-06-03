using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Clients.Commands;

[PublicAPI]
public sealed record DeleteClientCommand(ClientId Id) : ICommand, IRequest<Result>;

public sealed class DeleteClientHandler(IClientRepository clientRepository, ITelemetryEventsCollector events)
    : IRequestHandler<DeleteClientCommand, Result>
{
    public async Task<Result> Handle(DeleteClientCommand command, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(command.Id, cancellationToken);
        if (client is null) return Result.NotFound($"Client with id '{command.Id}' not found.");

        clientRepository.Remove(client);
        events.CollectEvent(new ClientDeleted(client.Id));

        return Result.Success();
    }
}
