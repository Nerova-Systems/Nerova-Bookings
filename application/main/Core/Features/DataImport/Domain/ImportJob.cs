using System.Collections.Immutable;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.DataImport.Domain;

[PublicAPI]
[IdPrefix("impjb")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, ImportJobId>))]
public sealed record ImportJobId(string Value) : StronglyTypedUlid<ImportJobId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A tenant's client-list import (docs/agentic-system-spec.md §5.3): the uploaded file, the inferred
///     column mapping, and per-row normalization/validation results. The aggregate IS the pipeline
///     checkpoint — every step persists its output here, so any step can be re-run from the stored file
///     without a durable workflow runtime, and nothing touches the clients table until the tenant
///     approves the review.
/// </summary>
public sealed class ImportJob : AggregateRoot<ImportJobId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private ImportJob() : base(ImportJobId.NewId())
    {
        FileName = string.Empty;
        FileContent = string.Empty;
    }

    private ImportJob(TenantId tenantId, string fileName, string fileContent)
        : base(ImportJobId.NewId())
    {
        TenantId = tenantId;
        FileName = fileName.Trim();
        FileContent = fileContent;
        Status = ImportJobStatus.Parsing;
        Rows = [];
    }

    public string FileName { get; private set; }

    /// <summary>The raw uploaded CSV. The single source of truth every pipeline step re-reads from.</summary>
    public string FileContent { get; private set; }

    public ImportJobStatus Status { get; private set; }

    public ImportColumnMapping? ColumnMapping { get; private set; }

    public ImmutableArray<ImportRowResult> Rows { get; private set; }

    public int RowsTotal { get; private set; }

    public int RowsValid { get; private set; }

    public int RowsDuplicate { get; private set; }

    public int RowsInvalid { get; private set; }

    public int RowsCommitted { get; private set; }

    public UserId? ApprovedByUserId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static ImportJob Create(TenantId tenantId, string fileName, string fileContent)
    {
        return new ImportJob(tenantId, fileName, fileContent);
    }

    public void SetColumnMapping(ImportColumnMapping columnMapping)
    {
        ColumnMapping = columnMapping;
        Status = ImportJobStatus.Normalizing;
    }

    public void SetRows(ImmutableArray<ImportRowResult> rows)
    {
        Rows = rows;
        RowsTotal = rows.Length;
        RowsValid = rows.Count(row => row.Status == ImportRowStatus.Valid);
        RowsDuplicate = rows.Count(row => row.Status == ImportRowStatus.Duplicate);
        RowsInvalid = rows.Count(row => row.Status == ImportRowStatus.Invalid);
        Status = ImportJobStatus.ReadyForReview;
    }

    public void MarkCommitting(UserId approvedByUserId)
    {
        ApprovedByUserId = approvedByUserId;
        Status = ImportJobStatus.Committing;
    }

    public void MarkCompleted(int rowsCommitted)
    {
        RowsCommitted = rowsCommitted;
        Status = ImportJobStatus.Completed;
    }

    public void MarkRejected()
    {
        Status = ImportJobStatus.Rejected;
    }

    public void MarkFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = ImportJobStatus.Failed;
    }

    /// <summary>Resets a failed job so the pipeline re-runs from the stored file.</summary>
    public void Restart()
    {
        Status = ImportJobStatus.Parsing;
        ErrorMessage = null;
        ColumnMapping = null;
        Rows = [];
        RowsTotal = 0;
        RowsValid = 0;
        RowsDuplicate = 0;
        RowsInvalid = 0;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportJobStatus
{
    Parsing,
    Mapping,
    Normalizing,
    Validating,
    ReadyForReview,
    Committing,
    Completed,
    Failed,
    Rejected
}

/// <summary>
///     Which uploaded column feeds which client field, with per-column confidence (0..1) from the
///     inference step. A null column means the field is absent from the file. FullNameColumn is used
///     when the file has one combined name column that normalization splits.
/// </summary>
[PublicAPI]
public sealed record ImportColumnMapping(
    string? FirstNameColumn,
    string? LastNameColumn,
    string? FullNameColumn,
    string? EmailColumn,
    string? PhoneColumn,
    string? NotesColumn,
    double Confidence,
    string Source
);

[PublicAPI]
public sealed record ImportRowResult(
    int RowNumber,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    string? Notes,
    ImportRowStatus Status,
    string? Error,
    string? DuplicateClientId
);

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportRowStatus
{
    Valid,
    Duplicate,
    Invalid
}
