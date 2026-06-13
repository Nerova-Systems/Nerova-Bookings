using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Receptionist.Queries;

[PublicAPI]
public sealed record ClientAgentDetailResponse(string Key, string Label, string Value, bool IsConstraint, bool IsWritable);

[PublicAPI]
public sealed record GetClientAgentDetailsResponse(ClientAgentDetailResponse[] Details);

/// <summary>
///     Vertical field values the AI receptionist may see for an identified client
///     (docs/vertical-template-fields-spec.md §6): catalog entries with AgentAccess Read/ReadWrite only.
///     Sensitive-class fields are structurally excluded — they are never part of the catalog slice this
///     query reads, no matter the caller. Anonymous webhook path: explicit TenantId, unfiltered repos.
/// </summary>
[PublicAPI]
public sealed record GetClientAgentDetailsQuery(TenantId TenantId, ClientId ClientId) : IRequest<Result<GetClientAgentDetailsResponse>>;

public sealed class GetClientAgentDetailsHandler(
    IClientRepository clientRepository,
    ISchedulingProfileRepository schedulingProfileRepository
) : IRequestHandler<GetClientAgentDetailsQuery, Result<GetClientAgentDetailsResponse>>
{
    public async Task<Result<GetClientAgentDetailsResponse>> Handle(GetClientAgentDetailsQuery query, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdUnfilteredAsync(query.TenantId, query.ClientId, cancellationToken);
        if (client is null) return Result<GetClientAgentDetailsResponse>.NotFound($"Client with id '{query.ClientId}' not found.");

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(query.TenantId, cancellationToken);
        var vertical = profile?.Vertical;
        if (vertical is null or NerovaVertical.Other)
        {
            return new GetClientAgentDetailsResponse([]);
        }

        var values = client.GetVerticalFields();
        var details = VerticalFieldCatalog.For(vertical.Value)
            .Where(definition => definition.AgentAccess != VerticalFieldAgentAccess.None)
            .Where(definition => values.ContainsKey(definition.Key))
            .Select(definition => new ClientAgentDetailResponse(
                    definition.Key,
                    definition.Label,
                    values[definition.Key],
                    definition.Sensitivity == VerticalFieldSensitivity.Constraint,
                    definition.AgentAccess == VerticalFieldAgentAccess.ReadWrite
                )
            )
            .ToArray();

        return new GetClientAgentDetailsResponse(details);
    }
}
