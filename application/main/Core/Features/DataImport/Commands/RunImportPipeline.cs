using System.Collections.Immutable;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.DataImport.Agent;
using Main.Features.DataImport.Domain;
using Main.Features.DataImport.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Telemetry;

namespace Main.Features.DataImport.Commands;

/// <summary>
///     Runs the import pipeline from the stored file to the review gate: Parse → InferColumnMapping
///     (agent with deterministic fallback) → NormalizeRows (E.164 phones, name splitting) → ValidateRows
///     (duplicates against existing clients and within the file) → ReadyForReview. Re-runnable: a failed
///     or stuck job restarts from the file because the aggregate is the only checkpoint (spec R17).
/// </summary>
[PublicAPI]
public sealed record RunImportPipelineCommand(ImportJobId ImportJobId) : ICommand, IRequest<Result>;

public sealed class RunImportPipelineHandler(
    IImportJobRepository importJobRepository,
    IClientRepository clientRepository,
    ColumnMappingInferrer columnMappingInferrer,
    ITelemetryEventsCollector events,
    ILogger<RunImportPipelineHandler> logger
) : IRequestHandler<RunImportPipelineCommand, Result>
{
    public async Task<Result> Handle(RunImportPipelineCommand command, CancellationToken cancellationToken)
    {
        var importJob = await importJobRepository.GetByIdAsync(command.ImportJobId, cancellationToken);
        if (importJob is null)
        {
            return Result.NotFound($"Import job '{command.ImportJobId}' was not found.");
        }

        if (importJob.Status is ImportJobStatus.Completed or ImportJobStatus.Committing or ImportJobStatus.Rejected)
        {
            return Result.BadRequest("Import job has already been processed.");
        }

        if (importJob.Status is ImportJobStatus.Failed or ImportJobStatus.ReadyForReview)
        {
            importJob.Restart();
        }

        try
        {
            var document = CsvParser.Parse(importJob.FileContent);
            if (document.Headers.Length == 0 || document.Rows.Length == 0)
            {
                importJob.MarkFailed("The file contains no data rows.");
                importJobRepository.Update(importJob);
                return Result.Success();
            }

            var mapping = await columnMappingInferrer.InferAsync(document.Headers, document.Rows, cancellationToken);
            if (mapping.FirstNameColumn is null && mapping.FullNameColumn is null)
            {
                importJob.MarkFailed("No name column could be identified in the file.");
                importJobRepository.Update(importJob);
                return Result.Success();
            }

            importJob.SetColumnMapping(mapping);

            var headerIndex = document.Headers
                .Select((header, index) => (header, index))
                .ToDictionary(pair => pair.header, pair => pair.index, StringComparer.OrdinalIgnoreCase);

            var existingClients = await clientRepository.GetAllForDuplicateCheckUnfilteredAsync(importJob.TenantId, cancellationToken);
            var existingByPhone = existingClients.Where(client => client.PhoneNumber is not null).GroupBy(client => client.PhoneNumber!).ToDictionary(group => group.Key, group => group.First());
            var existingByEmail = existingClients.Where(client => client.Email is not null).GroupBy(client => client.Email!).ToDictionary(group => group.Key, group => group.First());

            var seenPhones = new HashSet<string>(StringComparer.Ordinal);
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rows = ImmutableArray.CreateBuilder<ImportRowResult>(document.Rows.Length);
            for (var rowIndex = 0; rowIndex < document.Rows.Length; rowIndex++)
            {
                rows.Add(NormalizeAndValidateRow(
                        rowNumber: rowIndex + 1,
                        document.Rows[rowIndex],
                        mapping,
                        headerIndex,
                        existingByPhone,
                        existingByEmail,
                        seenPhones,
                        seenEmails
                    )
                );
            }

            importJob.SetRows(rows.ToImmutable());
            importJobRepository.Update(importJob);

            events.CollectEvent(new ImportJobReadyForReview(importJob.Id, importJob.RowsTotal, importJob.RowsValid, importJob.RowsDuplicate, importJob.RowsInvalid));

            return Result.Success();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Import pipeline failed for job {ImportJobId}", importJob.Id.Value);
            importJob.MarkFailed("The file could not be processed. Check that it is a valid CSV export.");
            importJobRepository.Update(importJob);
            return Result.Success();
        }
    }

    private static ImportRowResult NormalizeAndValidateRow(
        int rowNumber,
        string[] row,
        ImportColumnMapping mapping,
        Dictionary<string, int> headerIndex,
        Dictionary<string, Client> existingByPhone,
        Dictionary<string, Client> existingByEmail,
        HashSet<string> seenPhones,
        HashSet<string> seenEmails
    )
    {
        string? GetValue(string? column)
        {
            if (column is null || !headerIndex.TryGetValue(column, out var index) || index >= row.Length) return null;
            var value = row[index].Trim();
            return value.Length == 0 ? null : value;
        }

        var firstName = GetValue(mapping.FirstNameColumn);
        var lastName = GetValue(mapping.LastNameColumn);
        if (firstName is null && mapping.FullNameColumn is not null)
        {
            var fullName = GetValue(mapping.FullNameColumn);
            if (fullName is not null)
            {
                var spaceIndex = fullName.IndexOf(' ');
                firstName = spaceIndex < 0 ? fullName : fullName[..spaceIndex];
                lastName = spaceIndex < 0 ? null : fullName[(spaceIndex + 1)..].Trim();
            }
        }

        var email = GetValue(mapping.EmailColumn)?.ToLowerInvariant();
        var rawPhone = GetValue(mapping.PhoneColumn);
        var notes = GetValue(mapping.NotesColumn);

        if (firstName is null)
        {
            return new ImportRowResult(rowNumber, string.Empty, string.Empty, email, rawPhone, notes, ImportRowStatus.Invalid, "Missing name.", null);
        }

        var normalizedPhone = SouthAfricanPhoneNormalizer.Normalize(rawPhone);
        if (rawPhone is not null && normalizedPhone is null)
        {
            return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, email, rawPhone, notes, ImportRowStatus.Invalid, $"Phone number '{rawPhone}' is not a valid South African number.", null);
        }

        if (email is not null && (!email.Contains('@') || email.Length > 320))
        {
            return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, null, normalizedPhone, notes, ImportRowStatus.Invalid, $"Email '{email}' is not valid.", null);
        }

        if (email is null && normalizedPhone is null)
        {
            return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, null, null, notes, ImportRowStatus.Invalid, "Row has neither a phone number nor an email.", null);
        }

        if (normalizedPhone is not null && existingByPhone.TryGetValue(normalizedPhone, out var clientByPhone))
        {
            return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, email, normalizedPhone, notes, ImportRowStatus.Duplicate, "A client with this phone number already exists.", clientByPhone.Id.Value);
        }

        if (email is not null && existingByEmail.TryGetValue(email, out var clientByEmail))
        {
            return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, email, normalizedPhone, notes, ImportRowStatus.Duplicate, "A client with this email already exists.", clientByEmail.Id.Value);
        }

        if (normalizedPhone is not null && !seenPhones.Add(normalizedPhone) || email is not null && !seenEmails.Add(email))
        {
            return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, email, normalizedPhone, notes, ImportRowStatus.Duplicate, "Duplicated within the file.", null);
        }

        return new ImportRowResult(rowNumber, firstName, lastName ?? string.Empty, email, normalizedPhone, notes, ImportRowStatus.Valid, null, null);
    }
}
