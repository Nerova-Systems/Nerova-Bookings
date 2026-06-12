using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.DataImport.Commands;

[PublicAPI]
public sealed record ImportClientRow(string FirstName, string LastName, string? Email, string? PhoneNumber, string? Notes);

[PublicAPI]
public sealed record BulkUpsertClientsResponse(int UpsertedCount, int MergedCount);

/// <summary>
///     Idempotent batched client upsert (spec R20): rows matching an existing client by phone or email
///     merge into it (filling blanks, never destroying data); the rest are created. Loads the tenant's
///     clients once — no per-row queries. Re-running with the same rows creates no duplicates.
/// </summary>
[PublicAPI]
public sealed record BulkUpsertClientsCommand(ImportClientRow[] Rows) : ICommand, IRequest<Result<BulkUpsertClientsResponse>>;

public sealed class BulkUpsertClientsHandler(IClientRepository clientRepository, IExecutionContext executionContext, ITelemetryEventsCollector events)
    : IRequestHandler<BulkUpsertClientsCommand, Result<BulkUpsertClientsResponse>>
{
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
                clientRepository.Update(existing);
                mergedCount++;
                continue;
            }

            var client = Client.Create(tenantId, row.FirstName, row.LastName, row.Email, row.PhoneNumber);
            if (row.Notes is not null) client.UpdateNotes(row.Notes);
            await clientRepository.AddAsync(client, cancellationToken);

            if (client.PhoneNumber is not null) byPhone[client.PhoneNumber] = client;
            if (client.Email is not null) byEmail[client.Email] = client;
            createdCount++;
        }

        events.CollectEvent(new ClientsBulkImported(createdCount, mergedCount));

        return Result<BulkUpsertClientsResponse>.Success(new BulkUpsertClientsResponse(createdCount + mergedCount, mergedCount));
    }
}
