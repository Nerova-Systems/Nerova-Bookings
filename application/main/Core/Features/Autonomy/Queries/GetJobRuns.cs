using JetBrains.Annotations;
using Main.Features.Autonomy.Domain;
using Main.Features.Autonomy.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Autonomy.Queries;

[PublicAPI]
public sealed record JobRunResponse(
    JobRunId Id,
    string JobType,
    string Summary,
    JobRunStatus Status,
    string? Receipt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExecutedAt
);

[PublicAPI]
public sealed record GetJobRunsResponse(JobRunResponse[] JobRuns, int AwaitingApprovalCount);

/// <summary>The "Handled by Nerova" feed plus the suggestion inbox (design §A4/§A5).</summary>
[PublicAPI]
public sealed record GetJobRunsQuery(JobRunStatus? Status = null, int Limit = 50) : IRequest<Result<GetJobRunsResponse>>;

public sealed class GetJobRunsHandler(IJobRunRepository jobRunRepository, IExecutionContext executionContext)
    : IRequestHandler<GetJobRunsQuery, Result<GetJobRunsResponse>>
{
    public async Task<Result<GetJobRunsResponse>> Handle(GetJobRunsQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is null)
        {
            return Result<GetJobRunsResponse>.Unauthorized("Authentication is required.");
        }

        var limit = Math.Clamp(query.Limit, 1, 200);
        var jobRuns = await jobRunRepository.GetByTenantAsync(query.Status, limit, cancellationToken);
        var awaitingApproval = await jobRunRepository.GetByTenantAsync(JobRunStatus.AwaitingApproval, limit, cancellationToken);

        var responses = jobRuns
            .Select(run => new JobRunResponse(run.Id, run.JobType, run.Summary, run.Status, run.Receipt, run.CreatedAt, run.ExecutedAt))
            .ToArray();

        return Result<GetJobRunsResponse>.Success(new GetJobRunsResponse(responses, awaitingApproval.Length));
    }
}

[PublicAPI]
public sealed record JobPolicyResponse(string JobType, int Level, int ApprovalsStreak, bool PromotionOffered);

[PublicAPI]
public sealed record GetJobPoliciesResponse(JobPolicyResponse[] Policies);

/// <summary>The tenant's autonomy ladder: every registered job with its current level (design §0).</summary>
[PublicAPI]
public sealed record GetJobPoliciesQuery : IRequest<Result<GetJobPoliciesResponse>>;

public sealed class GetJobPoliciesHandler(
    ITenantJobPolicyRepository tenantJobPolicyRepository,
    IEnumerable<IAutonomyJob> autonomyJobs,
    IExecutionContext executionContext
) : IRequestHandler<GetJobPoliciesQuery, Result<GetJobPoliciesResponse>>
{
    public async Task<Result<GetJobPoliciesResponse>> Handle(GetJobPoliciesQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is null)
        {
            return Result<GetJobPoliciesResponse>.Unauthorized("Authentication is required.");
        }

        var policies = await tenantJobPolicyRepository.GetByTenantAsync(cancellationToken);
        var policiesByJobType = policies.ToDictionary(policy => policy.JobType);

        var responses = autonomyJobs
            .Select(job => policiesByJobType.TryGetValue(job.JobType, out var policy)
                ? new JobPolicyResponse(job.JobType, policy.Level, policy.ApprovalsStreak, policy.IsPromotionOffered)
                : new JobPolicyResponse(job.JobType, job.DefaultLevel, 0, false)
            )
            .OrderBy(response => response.JobType)
            .ToArray();

        return Result<GetJobPoliciesResponse>.Success(new GetJobPoliciesResponse(responses));
    }
}
