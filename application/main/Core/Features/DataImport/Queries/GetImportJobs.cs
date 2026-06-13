using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.DataImport.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.DataImport.Queries;

[PublicAPI]
public sealed record ImportColumnMappingResponse(
    string? FirstNameColumn,
    string? LastNameColumn,
    string? FullNameColumn,
    string? EmailColumn,
    string? PhoneColumn,
    string? NotesColumn,
    double Confidence,
    string Source,
    Dictionary<string, string>? VerticalFieldColumns,
    string[] SensitiveFieldKeys,
    string[] ConstraintFieldKeys
);

[PublicAPI]
public sealed record ImportRowResponse(
    int RowNumber,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    string? Notes,
    ImportRowStatus Status,
    string? Error,
    Dictionary<string, string>? VerticalFields,
    Dictionary<string, string>? SensitiveFields
);

[PublicAPI]
public sealed record ImportJobDetailsResponse(
    ImportJobId Id,
    string FileName,
    ImportJobStatus Status,
    ImportColumnMappingResponse? ColumnMapping,
    int RowsTotal,
    int RowsValid,
    int RowsDuplicate,
    int RowsInvalid,
    int RowsCommitted,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    ImportRowResponse[] Rows
);

[PublicAPI]
public sealed record GetImportJobQuery : IRequest<Result<ImportJobDetailsResponse>>
{
    [JsonIgnore] // Removes from API contract
    public ImportJobId Id { get; init; } = null!;
}

public sealed class GetImportJobHandler(
    IImportJobRepository importJobRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetImportJobQuery, Result<ImportJobDetailsResponse>>
{
    public async Task<Result<ImportJobDetailsResponse>> Handle(GetImportJobQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is null)
        {
            return Result<ImportJobDetailsResponse>.Unauthorized("Authentication is required.");
        }

        var importJob = await importJobRepository.GetByIdAsync(query.Id, cancellationToken);
        if (importJob is null)
        {
            return Result<ImportJobDetailsResponse>.NotFound($"Import job '{query.Id}' was not found.");
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(executionContext.TenantId!, cancellationToken);
        var catalog = profile?.Vertical is { } vertical ? VerticalFieldCatalog.For(vertical) : [];

        var mapping = importJob.ColumnMapping is null
            ? null
            : new ImportColumnMappingResponse(
                importJob.ColumnMapping.FirstNameColumn,
                importJob.ColumnMapping.LastNameColumn,
                importJob.ColumnMapping.FullNameColumn,
                importJob.ColumnMapping.EmailColumn,
                importJob.ColumnMapping.PhoneColumn,
                importJob.ColumnMapping.NotesColumn,
                importJob.ColumnMapping.Confidence,
                importJob.ColumnMapping.Source,
                importJob.ColumnMapping.VerticalFieldColumns,
                SensitiveKeysIn(importJob.ColumnMapping, catalog),
                ConstraintKeysIn(importJob.ColumnMapping, catalog)
            );

        // Sensitive values are masked in the review payload (vertical-template-fields-spec §7) — the
        // owner confirms per column knowing WHICH fields hold data, never seeing the values themselves.
        var rows = importJob.Rows
            .Select(row => new ImportRowResponse(
                    row.RowNumber, row.FirstName, row.LastName, row.Email, row.PhoneNumber, row.Notes, row.Status, row.Error,
                    row.VerticalFields,
                    row.SensitiveFields?.ToDictionary(pair => pair.Key, _ => "•••")
                )
            )
            .ToArray();

        return Result<ImportJobDetailsResponse>.Success(new ImportJobDetailsResponse(
                importJob.Id,
                importJob.FileName,
                importJob.Status,
                mapping,
                importJob.RowsTotal,
                importJob.RowsValid,
                importJob.RowsDuplicate,
                importJob.RowsInvalid,
                importJob.RowsCommitted,
                importJob.ErrorMessage,
                importJob.CreatedAt,
                rows
            )
        );
    }

    private static string[] SensitiveKeysIn(ImportColumnMapping mapping, IReadOnlyList<VerticalFieldDefinition> catalog)
    {
        if (mapping.VerticalFieldColumns is null) return [];
        return catalog
            .Where(definition => definition.Sensitivity == VerticalFieldSensitivity.Sensitive && mapping.VerticalFieldColumns.ContainsKey(definition.Key))
            .Select(definition => definition.Key)
            .ToArray();
    }

    private static string[] ConstraintKeysIn(ImportColumnMapping mapping, IReadOnlyList<VerticalFieldDefinition> catalog)
    {
        if (mapping.VerticalFieldColumns is null) return [];
        return catalog
            .Where(definition => definition.Sensitivity == VerticalFieldSensitivity.Constraint && mapping.VerticalFieldColumns.ContainsKey(definition.Key))
            .Select(definition => definition.Key)
            .ToArray();
    }
}

[PublicAPI]
public sealed record ImportJobSummaryResponse(
    ImportJobId Id,
    string FileName,
    ImportJobStatus Status,
    int RowsTotal,
    int RowsCommitted,
    DateTimeOffset CreatedAt
);

[PublicAPI]
public sealed record GetImportJobsResponse(ImportJobSummaryResponse[] ImportJobs);

[PublicAPI]
public sealed record GetImportJobsQuery : IRequest<Result<GetImportJobsResponse>>;

public sealed class GetImportJobsHandler(IImportJobRepository importJobRepository, IExecutionContext executionContext)
    : IRequestHandler<GetImportJobsQuery, Result<GetImportJobsResponse>>
{
    public async Task<Result<GetImportJobsResponse>> Handle(GetImportJobsQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is null)
        {
            return Result<GetImportJobsResponse>.Unauthorized("Authentication is required.");
        }

        var importJobs = await importJobRepository.GetByTenantAsync(cancellationToken);

        var summaries = importJobs
            .Select(job => new ImportJobSummaryResponse(job.Id, job.FileName, job.Status, job.RowsTotal, job.RowsCommitted, job.CreatedAt))
            .ToArray();

        return Result<GetImportJobsResponse>.Success(new GetImportJobsResponse(summaries));
    }
}
