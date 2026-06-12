using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Autonomy.Domain;
using Main.Features.Autonomy.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Autonomy.Commands;

/// <summary>
///     The owner's one-tap approval of a suggested action (the autonomy ladder's L1, design §0): executes
///     the parked run and counts the approval streak. After enough consecutive approvals the response
///     surfaces a promotion offer — "you've approved this 5 times, want me to just handle it?" — which the
///     owner accepts via SetJobPolicyLevel.
/// </summary>
[PublicAPI]
public sealed record ApproveJobRunResponse(string Receipt, bool PromotionOffered);

[PublicAPI]
public sealed record ApproveJobRunCommand : ICommand, IRequest<Result<ApproveJobRunResponse>>
{
    [JsonIgnore] // Removes from API contract
    public JobRunId Id { get; init; } = null!;
}

public sealed class ApproveJobRunHandler(
    IJobRunRepository jobRunRepository,
    ITenantJobPolicyRepository tenantJobPolicyRepository,
    IEnumerable<IAutonomyJob> autonomyJobs,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<ApproveJobRunCommand, Result<ApproveJobRunResponse>>
{
    public async Task<Result<ApproveJobRunResponse>> Handle(ApproveJobRunCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result<ApproveJobRunResponse>.Unauthorized("Authentication is required.");
        }

        var jobRun = await jobRunRepository.GetByIdAsync(command.Id, cancellationToken);
        if (jobRun is null || jobRun.TenantId != tenantId)
        {
            return Result<ApproveJobRunResponse>.NotFound($"Suggestion '{command.Id}' was not found.");
        }

        if (jobRun.Status != JobRunStatus.AwaitingApproval)
        {
            return Result<ApproveJobRunResponse>.BadRequest("This suggestion has already been handled.");
        }

        var job = autonomyJobs.FirstOrDefault(candidate => candidate.JobType == jobRun.JobType);
        if (job is null)
        {
            return Result<ApproveJobRunResponse>.BadRequest($"Job type '{jobRun.JobType}' is no longer available.");
        }

        jobRun.MarkExecuting();
        var executionResult = await job.ExecuteAsync(jobRun, cancellationToken);
        if (!executionResult.IsSuccess)
        {
            jobRun.Skip(executionResult.GetErrorSummary());
            jobRunRepository.Update(jobRun);
            return Result<ApproveJobRunResponse>.BadRequest(executionResult.GetErrorSummary(), commitChanges: true);
        }

        jobRun.Complete(executionResult.Value!, timeProvider.GetUtcNow());
        jobRunRepository.Update(jobRun);

        var policy = await tenantJobPolicyRepository.GetByJobTypeUnfilteredAsync(tenantId, jobRun.JobType, cancellationToken);
        var policyIsNew = policy is null;
        policy ??= TenantJobPolicy.CreateDefault(tenantId, jobRun.JobType, job.DefaultLevel);
        policy.RecordApproval();
        if (policyIsNew)
        {
            await tenantJobPolicyRepository.AddAsync(policy, cancellationToken);
        }
        else
        {
            tenantJobPolicyRepository.Update(policy);
        }

        events.CollectEvent(new JobSuggestionResolved(jobRun.JobType, true));

        return Result<ApproveJobRunResponse>.Success(new ApproveJobRunResponse(executionResult.Value!, policy.IsPromotionOffered));
    }
}

/// <summary>Dismisses a suggested action without executing it and resets the approval streak.</summary>
[PublicAPI]
public sealed record DismissJobRunCommand : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes from API contract
    public JobRunId Id { get; init; } = null!;
}

public sealed class DismissJobRunHandler(
    IJobRunRepository jobRunRepository,
    ITenantJobPolicyRepository tenantJobPolicyRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DismissJobRunCommand, Result>
{
    public async Task<Result> Handle(DismissJobRunCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var jobRun = await jobRunRepository.GetByIdAsync(command.Id, cancellationToken);
        if (jobRun is null || jobRun.TenantId != tenantId)
        {
            return Result.NotFound($"Suggestion '{command.Id}' was not found.");
        }

        if (jobRun.Status != JobRunStatus.AwaitingApproval)
        {
            return Result.BadRequest("This suggestion has already been handled.");
        }

        jobRun.Skip("Dismissed by the owner.");
        jobRunRepository.Update(jobRun);

        var policy = await tenantJobPolicyRepository.GetByJobTypeUnfilteredAsync(tenantId, jobRun.JobType, cancellationToken);
        if (policy is not null)
        {
            policy.RecordDismissal();
            tenantJobPolicyRepository.Update(policy);
        }

        events.CollectEvent(new JobSuggestionResolved(jobRun.JobType, false));

        return Result.Success();
    }
}

/// <summary>
///     Moves a job type up or down the autonomy ladder for the tenant (L0 off → L1 suggest → L2 act+tell
///     → L3 act quietly). Promotion requires the owner's explicit tap; demotion is instant.
/// </summary>
[PublicAPI]
public sealed record SetJobPolicyLevelCommand(string JobType, int Level) : ICommand, IRequest<Result>;

public sealed class SetJobPolicyLevelValidator : AbstractValidator<SetJobPolicyLevelCommand>
{
    public SetJobPolicyLevelValidator()
    {
        RuleFor(command => command.JobType).NotEmpty().MaximumLength(100).WithMessage("Job type is required.");
        RuleFor(command => command.Level).InclusiveBetween(0, 3).WithMessage("Level must be between 0 and 3.");
    }
}

public sealed class SetJobPolicyLevelHandler(
    ITenantJobPolicyRepository tenantJobPolicyRepository,
    IEnumerable<IAutonomyJob> autonomyJobs,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<SetJobPolicyLevelCommand, Result>
{
    public async Task<Result> Handle(SetJobPolicyLevelCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var job = autonomyJobs.FirstOrDefault(candidate => candidate.JobType == command.JobType);
        if (job is null)
        {
            return Result.BadRequest($"Job type '{command.JobType}' does not exist.");
        }

        var policy = await tenantJobPolicyRepository.GetByJobTypeUnfilteredAsync(tenantId, command.JobType, cancellationToken);
        var fromLevel = policy?.Level ?? job.DefaultLevel;
        var policyIsNew = policy is null;
        policy ??= TenantJobPolicy.CreateDefault(tenantId, command.JobType, job.DefaultLevel);
        policy.SetLevel(command.Level);

        if (policyIsNew)
        {
            await tenantJobPolicyRepository.AddAsync(policy, cancellationToken);
        }
        else
        {
            tenantJobPolicyRepository.Update(policy);
        }

        events.CollectEvent(new AutonomyLevelChanged(command.JobType, fromLevel, command.Level));

        return Result.Success();
    }
}
