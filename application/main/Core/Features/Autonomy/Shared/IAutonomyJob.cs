using Main.Features.Autonomy.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Autonomy.Shared;

/// <summary>One detection produced by a job's detector: the trigger, the proposal, and execution input.</summary>
public sealed record AutonomyDetection(string TriggerReference, string Summary, string? PayloadJson);

/// <summary>
///     An autonomous front-desk job (docs/maf-autonomy-design.md §A1): a deterministic detector that
///     decides whether and for whom the job applies, and an executor whose actions are existing commands.
///     The model never detects; jobs are code, not config.
/// </summary>
public interface IAutonomyJob
{
    string JobType { get; }

    /// <summary>The trust level new tenants start at (L1 = suggest; the owner taps to execute).</summary>
    int DefaultLevel { get; }

    Task<AutonomyDetection[]> DetectAsync(TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<Result<string>> ExecuteAsync(JobRun jobRun, CancellationToken cancellationToken);
}

public static class AutonomyJobTypes
{
    public const string PaymentRecovery = "payment-recovery";
    public const string RebookCancelled = "rebook-cancelled";
    public const string WeeklyDigest = "weekly-digest";
}
