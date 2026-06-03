using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Clients.Commands;

[PublicAPI]
public sealed record BulkDeleteClientsCommand(ClientId[] ClientIds) : ICommand, IRequest<Result>;

public sealed class BulkDeleteClientsValidator : AbstractValidator<BulkDeleteClientsCommand>
{
    public BulkDeleteClientsValidator()
    {
        RuleFor(x => x.ClientIds)
            .NotEmpty()
            .WithMessage("At least one client must be selected for deletion.")
            .Must(ids => ids.Length <= 100)
            .WithMessage("Cannot delete more than 100 clients at once.");
    }
}

public sealed class BulkDeleteClientsHandler(IClientRepository clientRepository, ITelemetryEventsCollector events)
    : IRequestHandler<BulkDeleteClientsCommand, Result>
{
    public async Task<Result> Handle(BulkDeleteClientsCommand command, CancellationToken cancellationToken)
    {
        var clientsToDelete = await clientRepository.GetByIdsAsync(command.ClientIds, cancellationToken);

        var missingClientIds = command.ClientIds.Where(id => !clientsToDelete.Select(c => c.Id).Contains(id)).ToArray();
        if (missingClientIds.Length > 0)
        {
            return Result.NotFound($"Clients with ids '{string.Join(", ", missingClientIds.Select(id => id.ToString()))}' not found.");
        }

        clientRepository.RemoveRange(clientsToDelete);
        foreach (var client in clientsToDelete)
        {
            events.CollectEvent(new ClientDeleted(client.Id, true));
        }

        events.CollectEvent(new ClientsBulkDeleted(command.ClientIds.Length));

        return Result.Success();
    }
}
