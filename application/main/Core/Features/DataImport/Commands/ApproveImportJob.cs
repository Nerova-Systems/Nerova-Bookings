using FluentValidation;
using JetBrains.Annotations;
using Main.Features.DataImport.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.DataImport.Commands;

/// <summary>
///     The tenant's one-tap approval of a reviewed import (spec R20/R21): commits valid rows through the
///     idempotent bulk upsert, optionally excluding flagged rows. Duplicate rows merge into the existing
///     client; re-approving a committed job creates no duplicates. Sensitive-class columns are never
///     committed implicitly: each sensitive field key must be explicitly confirmed
///     (docs/vertical-template-fields-spec.md §7) or its values are dropped, not imported.
/// </summary>
[PublicAPI]
public sealed record ApproveImportJobCommand(int[]? ExcludeRowNumbers = null, string[]? ConfirmedSensitiveFieldKeys = null) : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes from API contract
    public ImportJobId Id { get; init; } = null!;
}

public sealed class ApproveImportJobValidator : AbstractValidator<ApproveImportJobCommand>
{
    public ApproveImportJobValidator()
    {
        RuleFor(command => command.ExcludeRowNumbers).Must(rows => rows is null || rows.Length <= 100_000).WithMessage("Too many excluded rows.");
    }
}

public sealed class ApproveImportJobHandler(
    IImportJobRepository importJobRepository,
    IExecutionContext executionContext,
    IMediator mediator,
    ITelemetryEventsCollector events
) : IRequestHandler<ApproveImportJobCommand, Result>
{
    public async Task<Result> Handle(ApproveImportJobCommand command, CancellationToken cancellationToken)
    {
        var userId = executionContext.UserInfo.Id;
        if (userId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var importJob = await importJobRepository.GetByIdAsync(command.Id, cancellationToken);
        if (importJob is null)
        {
            return Result.NotFound($"Import job '{command.Id}' was not found.");
        }

        if (importJob.Status != ImportJobStatus.ReadyForReview)
        {
            return Result.BadRequest("Only an import that is ready for review can be approved.");
        }

        var excludedRows = command.ExcludeRowNumbers?.ToHashSet() ?? [];
        var confirmedSensitiveKeys = command.ConfirmedSensitiveFieldKeys?.ToHashSet(StringComparer.Ordinal) ?? [];
        var rowsToCommit = importJob.Rows
            .Where(row => row.Status != ImportRowStatus.Invalid && !excludedRows.Contains(row.RowNumber))
            .Select(row => new ImportClientRow(
                    row.FirstName,
                    row.LastName,
                    row.Email,
                    row.PhoneNumber,
                    row.Notes,
                    row.VerticalFields,
                    FilterConfirmed(row.SensitiveFields, confirmedSensitiveKeys)
                )
            )
            .ToArray();

        importJob.MarkCommitting(userId);

        var upsertResult = await mediator.Send(new BulkUpsertClientsCommand(rowsToCommit), cancellationToken);
        if (!upsertResult.IsSuccess)
        {
            return Result.From(upsertResult);
        }

        importJob.MarkCompleted(upsertResult.Value!.UpsertedCount);
        importJobRepository.Update(importJob);

        events.CollectEvent(new ImportJobCompleted(importJob.Id, importJob.RowsTotal, upsertResult.Value.UpsertedCount, importJob.RowsInvalid + excludedRows.Count));

        return Result.Success();
    }

    private static Dictionary<string, string>? FilterConfirmed(Dictionary<string, string>? sensitiveFields, HashSet<string> confirmedKeys)
    {
        if (sensitiveFields is null || confirmedKeys.Count == 0) return null;

        var confirmed = sensitiveFields.Where(pair => confirmedKeys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        return confirmed.Count == 0 ? null : confirmed;
    }
}
