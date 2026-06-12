using FluentValidation;
using JetBrains.Annotations;
using Main.Features.DataImport.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.DataImport.Commands;

[PublicAPI]
public sealed record StartImportJobResponse(ImportJobId Id);

/// <summary>
///     Accepts a tenant's client-list CSV upload and runs the import pipeline to the review gate
///     (spec R17/R18). Nothing is written to the clients table — the tenant reviews and approves first.
/// </summary>
[PublicAPI]
public sealed record StartImportJobCommand(string FileName, string FileContent) : ICommand, IRequest<Result<StartImportJobResponse>>;

public sealed class StartImportJobValidator : AbstractValidator<StartImportJobCommand>
{
    public StartImportJobValidator()
    {
        RuleFor(command => command.FileName).NotEmpty().MaximumLength(255).WithMessage("File name must be between 1 and 255 characters.");
        RuleFor(command => command.FileContent).NotEmpty().Must(content => content.Length <= 5_000_000).WithMessage("The file must contain data and be smaller than 5 MB.");
    }
}

public sealed class StartImportJobHandler(
    IImportJobRepository importJobRepository,
    IExecutionContext executionContext,
    IMediator mediator,
    ITelemetryEventsCollector events
) : IRequestHandler<StartImportJobCommand, Result<StartImportJobResponse>>
{
    public async Task<Result<StartImportJobResponse>> Handle(StartImportJobCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result<StartImportJobResponse>.Unauthorized("Authentication is required.");
        }

        var importJob = ImportJob.Create(tenantId, command.FileName, command.FileContent);
        await importJobRepository.AddAsync(importJob, cancellationToken);

        var pipelineResult = await mediator.Send(new RunImportPipelineCommand(importJob.Id), cancellationToken);
        if (!pipelineResult.IsSuccess)
        {
            return Result<StartImportJobResponse>.From(pipelineResult);
        }

        events.CollectEvent(new ImportJobStarted(importJob.Id));

        return Result<StartImportJobResponse>.Success(new StartImportJobResponse(importJob.Id));
    }
}
