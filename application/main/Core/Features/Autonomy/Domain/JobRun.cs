using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Autonomy.Domain;

[PublicAPI]
[IdPrefix("jobrn")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, JobRunId>))]
public sealed record JobRunId(string Value) : StronglyTypedUlid<JobRunId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     One execution of an autonomous front-desk job (docs/maf-autonomy-design.md §A3): what was
///     detected, what was (or would be) done, at which autonomy level, and the human-readable receipt
///     that feeds the "Handled by Nerova" feed. <see cref="TriggerReference" /> is unique per job type so
///     a detection spawns at most one run, making the runner idempotent across ticks.
/// </summary>
public sealed class JobRun : AggregateRoot<JobRunId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private JobRun() : base(JobRunId.NewId())
    {
        JobType = string.Empty;
        TriggerReference = string.Empty;
        Summary = string.Empty;
    }

    private JobRun(TenantId tenantId, string jobType, string triggerReference, string summary, string? payloadJson, int levelAtRun)
        : base(JobRunId.NewId())
    {
        TenantId = tenantId;
        JobType = jobType;
        TriggerReference = triggerReference;
        Summary = summary;
        PayloadJson = payloadJson;
        LevelAtRun = levelAtRun;
        Status = JobRunStatus.Detected;
    }

    public string JobType { get; private set; }

    /// <summary>Idempotency key: one run per (job type, trigger), e.g. a booking id or an ISO week.</summary>
    public string TriggerReference { get; private set; }

    /// <summary>Owner-facing description of what was detected and what the job proposes to do.</summary>
    public string Summary { get; private set; }

    /// <summary>Job-specific execution input (e.g. the booking id to remind), serialized JSON.</summary>
    public string? PayloadJson { get; private set; }

    public JobRunStatus Status { get; private set; }

    public int LevelAtRun { get; private set; }

    /// <summary>Human-readable proof-of-work shown in the feed and digest, written on completion.</summary>
    public string? Receipt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static JobRun Detect(TenantId tenantId, string jobType, string triggerReference, string summary, string? payloadJson, int levelAtRun)
    {
        return new JobRun(tenantId, jobType, triggerReference, summary, payloadJson, levelAtRun);
    }

    public void AwaitApproval()
    {
        Status = JobRunStatus.AwaitingApproval;
    }

    public void MarkExecuting()
    {
        Status = JobRunStatus.Executing;
    }

    public void Complete(string receipt, DateTimeOffset executedAt)
    {
        Status = JobRunStatus.Completed;
        Receipt = receipt;
        ExecutedAt = executedAt;
    }

    public void Skip(string reason)
    {
        Status = JobRunStatus.Skipped;
        Receipt = reason;
    }

    public void Fail(string errorMessage)
    {
        Status = JobRunStatus.Failed;
        ErrorMessage = errorMessage;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobRunStatus
{
    Detected,
    AwaitingApproval,
    Executing,
    Completed,
    Skipped,
    Failed
}

[PublicAPI]
[IdPrefix("jobpl")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, TenantJobPolicyId>))]
public sealed record TenantJobPolicyId(string Value) : StronglyTypedUlid<TenantJobPolicyId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A tenant's autonomy level for one job type (docs/maf-autonomy-design.md §0): L0 off, L1 suggest
///     (owner taps to execute), L2 act + tell, L3 act quietly. Promotion is earned — the approval streak
///     counts consecutive approved suggestions, and at the threshold the product offers "want me to just
///     handle it from now on?". Demotion is always one tap.
/// </summary>
public sealed class TenantJobPolicy : AggregateRoot<TenantJobPolicyId>, ITenantScopedEntity
{
    public const int PromotionStreakThreshold = 5;
    public const int DefaultDailyActionCap = 25;

    [UsedImplicitly]
    private TenantJobPolicy() : base(TenantJobPolicyId.NewId())
    {
        JobType = string.Empty;
    }

    private TenantJobPolicy(TenantId tenantId, string jobType, int level)
        : base(TenantJobPolicyId.NewId())
    {
        TenantId = tenantId;
        JobType = jobType;
        Level = level;
        DailyActionCap = DefaultDailyActionCap;
    }

    public string JobType { get; private set; }

    public int Level { get; private set; }

    public int ApprovalsStreak { get; private set; }

    public int DailyActionCap { get; private set; }

    /// <summary>True when the streak has earned a promotion offer (still requires the owner's tap).</summary>
    public bool IsPromotionOffered => Level == 1 && ApprovalsStreak >= PromotionStreakThreshold;

    public TenantId TenantId { get; } = new(0);

    public static TenantJobPolicy CreateDefault(TenantId tenantId, string jobType, int level)
    {
        return new TenantJobPolicy(tenantId, jobType, level);
    }

    public void SetLevel(int level)
    {
        Level = Math.Clamp(level, 0, 3);
        ApprovalsStreak = 0;
    }

    public void RecordApproval()
    {
        ApprovalsStreak++;
    }

    public void RecordDismissal()
    {
        ApprovalsStreak = 0;
    }
}
