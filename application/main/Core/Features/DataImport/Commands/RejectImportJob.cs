using JetBrains.Annotations;
using Main.Features.DataImport.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.DataImport.Commands;

/// <summary>Rejects a reviewed import: nothing is committed and the job is closed (spec R21).</summary>
[PublicAPI]
public sealed record RejectImportJobCommand : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes from API contract
    public ImportJobId Id { get; init; } = null!;
}

public sealed class RejectImportJobHandler(IImportJobRepository importJobRepository, IExecutionContext executionContext, ITelemetryEventsCollector events)
    : IRequestHandler<RejectImportJobCommand, Result>
{
    public async Task<Result> Handle(RejectImportJobCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Id is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var importJob = await importJobRepository.GetByIdAsync(command.Id, cancellationToken);
        if (importJob is null)
        {
            return Result.NotFound($"Import job '{command.Id}' was not found.");
        }

        if (importJob.Status is not (ImportJobStatus.ReadyForReview or ImportJobStatus.Failed))
        {
            return Result.BadRequest("Only an import awaiting review can be rejected.");
        }

        importJob.MarkRejected();
        importJobRepository.Update(importJob);

        events.CollectEvent(new ImportJobRejected(importJob.Id));

        return Result.Success();
    }
}
