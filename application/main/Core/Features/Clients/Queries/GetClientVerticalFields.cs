using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.Clients.Infrastructure;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Clients.Queries;

[PublicAPI]
public sealed record ClientVerticalFieldResponse(
    string Key,
    string Label,
    string Kind,
    string Sensitivity,
    string[] Options,
    string? Value
);

[PublicAPI]
public sealed record ClientVerticalFieldsResponse(
    NerovaVertical? Vertical,
    ClientVerticalFieldResponse[] Fields,
    string[] SensitiveFieldKeysWithValues
);

/// <summary>
///     The client profile "Details" card data (docs/vertical-template-fields-spec.md §10): the tenant
///     vertical's full field catalog in render order merged with this client's values. Sensitive-class
///     values are never returned here — only which sensitive keys hold a value, so the UI can show the
///     role-gated section without exposing anything.
/// </summary>
[PublicAPI]
public sealed record GetClientVerticalFieldsQuery(ClientId Id) : IRequest<Result<ClientVerticalFieldsResponse>>;

public sealed class GetClientVerticalFieldsHandler(
    IClientRepository clientRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    FieldProtector fieldProtector,
    IExecutionContext executionContext
) : IRequestHandler<GetClientVerticalFieldsQuery, Result<ClientVerticalFieldsResponse>>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public async Task<Result<ClientVerticalFieldsResponse>> Handle(GetClientVerticalFieldsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<ClientVerticalFieldsResponse>.Unauthorized("Authentication is required.");

        var client = await clientRepository.GetByIdAsync(query.Id, cancellationToken);
        if (client is null) return Result<ClientVerticalFieldsResponse>.NotFound($"Client with id '{query.Id}' not found.");

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(tenantId, cancellationToken);
        var vertical = profile?.Vertical;
        if (vertical is null or NerovaVertical.Other)
        {
            return new ClientVerticalFieldsResponse(vertical, [], []);
        }

        var values = client.GetVerticalFields();
        var fields = VerticalFieldCatalog.For(vertical.Value)
            .Where(definition => definition.Sensitivity != VerticalFieldSensitivity.Sensitive)
            .Select(definition => new ClientVerticalFieldResponse(
                    definition.Key,
                    definition.Label,
                    definition.Kind.ToString(),
                    definition.Sensitivity.ToString(),
                    definition.Options,
                    values.TryGetValue(definition.Key, out var value) ? value : null
                )
            )
            .ToArray();

        var sensitiveKeysWithValues = Array.Empty<string>();
        if (client.SensitiveFields is not null)
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(fieldProtector.Unprotect(client.SensitiveFields), JsonOptions);
            sensitiveKeysWithValues = payload?.Keys.ToArray() ?? [];
        }

        return new ClientVerticalFieldsResponse(vertical, fields, sensitiveKeysWithValues);
    }
}

[PublicAPI]
public sealed record ClientSensitiveFieldsResponse(Dictionary<string, string> Fields);

/// <summary>
///     Decrypts and returns the client's Sensitive-class field values. Owner/Admin only; every read is
///     collected as a <see cref="SensitiveFieldAccessed" /> audit event (field keys and role — never
///     values), per docs/vertical-template-fields-spec.md §3.
/// </summary>
[PublicAPI]
public sealed record GetClientSensitiveFieldsQuery(ClientId Id) : ICommand, IRequest<Result<ClientSensitiveFieldsResponse>>;

public sealed class GetClientSensitiveFieldsHandler(
    IClientRepository clientRepository,
    FieldProtector fieldProtector,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<GetClientSensitiveFieldsQuery, Result<ClientSensitiveFieldsResponse>>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public async Task<Result<ClientSensitiveFieldsResponse>> Handle(GetClientSensitiveFieldsQuery query, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<ClientSensitiveFieldsResponse>.Forbidden("Only owners and admins can view sensitive client fields.");
        }

        var client = await clientRepository.GetByIdAsync(query.Id, cancellationToken);
        if (client is null) return Result<ClientSensitiveFieldsResponse>.NotFound($"Client with id '{query.Id}' not found.");

        var payload = client.SensitiveFields is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(fieldProtector.Unprotect(client.SensitiveFields), JsonOptions) ?? new Dictionary<string, string>();

        if (payload.Count > 0)
        {
            events.CollectEvent(new SensitiveFieldAccessed(string.Join(",", payload.Keys), executionContext.UserInfo.Role ?? "Unknown"));
        }

        return new ClientSensitiveFieldsResponse(payload);
    }
}
