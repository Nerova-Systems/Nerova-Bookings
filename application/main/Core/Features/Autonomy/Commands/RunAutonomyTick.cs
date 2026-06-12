using JetBrains.Annotations;
using Main.Features.Autonomy.Domain;
using Main.Features.Autonomy.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Autonomy.Commands;

/// <summary>
///     One tick of the autonomy runner (design §1): for every tenant with a scheduling profile, runs each
///     registered job's deterministic detector, creates one <see cref="JobRun" /> per new trigger, and
///     either executes immediately (L2/L3, inside quiet hours, under the daily cap) or parks the run as a
///     suggestion awaiting the owner's tap (L1 — and any capped/after-hours run silently downgrades to a
///     suggestion rather than dropping, design §6.8/§6.9).
/// </summary>
[PublicAPI]
public sealed record RunAutonomyTickCommand(TenantId? OnlyTenantId = null, bool BypassQuietHours = false) : ICommand, IRequest<Result>;

public sealed class RunAutonomyTickHandler(
    IEnumerable<IAutonomyJob> autonomyJobs,
    IJobRunRepository jobRunRepository,
    ITenantJobPolicyRepository tenantJobPolicyRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    ILogger<RunAutonomyTickHandler> logger
) : IRequestHandler<RunAutonomyTickCommand, Result>
{
    private const int QuietHoursStartLocal = 8;
    private const int QuietHoursEndLocal = 19;

    public async Task<Result> Handle(RunAutonomyTickCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tenantIds = command.OnlyTenantId is not null
            ? [command.OnlyTenantId]
            : await schedulingProfileRepository.GetAllTenantIdsUnfilteredAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            foreach (var job in autonomyJobs)
            {
                try
                {
                    await RunJobForTenantAsync(job, tenantId, now, command.BypassQuietHours, cancellationToken);
                }
                catch (Exception exception)
                {
                    // One broken job/tenant pair must not stop the tick for everyone else.
                    logger.LogError(exception, "Autonomy job {JobType} failed for tenant {TenantId}", job.JobType, tenantId);
                }
            }
        }

        return Result.Success();
    }

    private async Task RunJobForTenantAsync(IAutonomyJob job, TenantId tenantId, DateTimeOffset now, bool bypassQuietHours, CancellationToken cancellationToken)
    {
        var policy = await tenantJobPolicyRepository.GetByJobTypeUnfilteredAsync(tenantId, job.JobType, cancellationToken);
        var level = policy?.Level ?? job.DefaultLevel;
        if (level == 0) return;

        var detections = await job.DetectAsync(tenantId, now, cancellationToken);
        foreach (var detection in detections)
        {
            var alreadyRan = await jobRunRepository.ExistsForTriggerUnfilteredAsync(tenantId, job.JobType, detection.TriggerReference, cancellationToken);
            if (alreadyRan) continue;

            var jobRun = JobRun.Detect(tenantId, job.JobType, detection.TriggerReference, detection.Summary, detection.PayloadJson, level);
            await jobRunRepository.AddAsync(jobRun, cancellationToken);

            var withinQuietHours = bypassQuietHours || IsWithinSendWindow(now);
            var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
            var executedToday = await jobRunRepository.CountExecutedSinceUnfilteredAsync(tenantId, dayStart, cancellationToken);
            var underDailyCap = executedToday < (policy?.DailyActionCap ?? TenantJobPolicy.DefaultDailyActionCap);

            if (level >= 2 && withinQuietHours && underDailyCap)
            {
                jobRun.MarkExecuting();
                var executionResult = await job.ExecuteAsync(jobRun, cancellationToken);
                if (executionResult.IsSuccess)
                {
                    jobRun.Complete(executionResult.Value!, now);
                }
                else
                {
                    jobRun.Skip(executionResult.GetErrorSummary());
                }

                jobRunRepository.Update(jobRun);
                events.CollectEvent(new JobRunCompleted(job.JobType, level, jobRun.Status.ToString()));
            }
            else
            {
                jobRun.AwaitApproval();
                jobRunRepository.Update(jobRun);
            }
        }
    }

    /// <summary>Customer-facing autonomous sends only between 08:00 and 19:00 South African time (design §6.9).</summary>
    private static bool IsWithinSendWindow(DateTimeOffset nowUtc)
    {
        var localHour = nowUtc.ToOffset(TimeSpan.FromHours(2)).Hour;
        return localHour is >= QuietHoursStartLocal and < QuietHoursEndLocal;
    }
}
