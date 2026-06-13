using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.Clients.Infrastructure;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.DataImport.Commands;

[PublicAPI]
public sealed record ImportClientRow(
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    string? Notes,
    Dictionary<string, string>? VerticalFields = null,
    Dictionary<string, string>? SensitiveFields = null
);

[PublicAPI]
public sealed record BulkUpsertClientsResponse(int UpsertedCount, int MergedCount);

/// <summary>
///     Idempotent batched client upsert (spec R20): rows matching an existing client by phone or email
///     merge into it (filling blanks, never destroying data); the rest are created. Loads the tenant's
///     clients once — no per-row queries. Re-running with the same rows creates no duplicates.
/// </summary>
[PublicAPI]
public sealed record BulkUpsertClientsCommand(ImportClientRow[] Rows) : ICommand, IRequest<Result<BulkUpsertClientsResponse>>;

public sealed class BulkUpsertClientsHandler(
    IClientRepository clientRepository,
    FieldProtector fieldProtector,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<BulkUpsertClientsCommand, Result<BulkUpsertClientsResponse>>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public async Task<Result<BulkUpsertClientsResponse>> Handle(BulkUpsertClientsCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result<BulkUpsertClientsResponse>.Unauthorized("Authentication is required.");
        }

        var existingClients = await clientRepository.GetAllForDuplicateCheckUnfilteredAsync(tenantId, cancellationToken);
        var byPhone = existingClients.Where(client => client.PhoneNumber is not null).GroupBy(client => client.PhoneNumber!).ToDictionary(group => group.Key, group => group.First());
        var byEmail = existingClients.Where(client => client.Email is not null).GroupBy(client => client.Email!, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var createdCount = 0;
        var mergedCount = 0;

        foreach (var row in command.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.FirstName)) continue;

            var existing = row.PhoneNumber is not null && byPhone.TryGetValue(row.PhoneNumber, out var clientByPhone) ? clientByPhone
                : row.Email is not null && byEmail.TryGetValue(row.Email, out var clientByEmail) ? clientByEmail
                : null;

            if (existing is not null)
            {
                // Merge: keep existing values, fill in blanks from the imported row.
                existing.Update(
                    existing.FirstName.Length > 0 ? existing.FirstName : row.FirstName,
                    existing.LastName.Length > 0 ? existing.LastName : row.LastName,
                    existing.Email ?? row.Email,
                    existing.PhoneNumber ?? row.PhoneNumber
                );
                if (existing.Notes is null && row.Notes is not null) existing.UpdateNotes(row.Notes);
                ApplyVerticalFields(existing, row);
                clientRepository.Update(existing);
                mergedCount++;
                continue;
            }

            var client = Client.Create(tenantId, row.FirstName, row.LastName, row.Email, row.PhoneNumber);
            if (row.Notes is not null) client.UpdateNotes(row.Notes);
            ApplyVerticalFields(client, row);
            await clientRepository.AddAsync(client, cancellationToken);

            if (client.PhoneNumber is not null) byPhone[client.PhoneNumber] = client;
            if (client.Email is not null) byEmail[client.Email] = client;
            createdCount++;
        }

        events.CollectEvent(new ClientsBulkImported(createdCount, mergedCount));

        return Result<BulkUpsertClientsResponse>.Success(new BulkUpsertClientsResponse(createdCount + mergedCount, mergedCount));
    }

    /// <summary>
    ///     Imports vertical field values with the same fill-blanks-only merge posture as core fields,
    ///     and lands confirmed Sensitive-class values encrypted (vertical-template-fields-spec §7).
    /// </summary>
    private void ApplyVerticalFields(Client client, ImportClientRow row)
    {
        if (row.VerticalFields is not null)
        {
            var current = client.GetVerticalFields();
            foreach (var (key, value) in row.VerticalFields)
            {
                if (!current.ContainsKey(key)) client.SetVerticalField(key, value);
            }
        }

        if (row.SensitiveFields is not null && row.SensitiveFields.Count > 0)
        {
            var payload = client.SensitiveFields is null
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(fieldProtector.Unprotect(client.SensitiveFields), JsonOptions) ?? new Dictionary<string, string>();

            var changed = false;
            foreach (var (key, value) in row.SensitiveFields)
            {
                if (payload.TryAdd(key, value)) changed = true;
            }

            if (changed)
            {
                client.SetSensitiveFieldsPayload(fieldProtector.Protect(JsonSerializer.Serialize(payload, JsonOptions)));
            }
        }
    }
}
