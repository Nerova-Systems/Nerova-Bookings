using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Mapster;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Clients.Queries;

[PublicAPI]
public sealed record GetClientByIdQuery(ClientId Id) : IRequest<Result<ClientDetails>>;

public sealed class GetClientByIdHandler(IClientRepository clientRepository)
    : IRequestHandler<GetClientByIdQuery, Result<ClientDetails>>
{
    public async Task<Result<ClientDetails>> Handle(GetClientByIdQuery query, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(query.Id, cancellationToken);

        if (client is null)
        {
            return Result<ClientDetails>.NotFound($"Client with ID '{query.Id}' not found.");
        }

        return client.Adapt<ClientDetails>();
    }
}
