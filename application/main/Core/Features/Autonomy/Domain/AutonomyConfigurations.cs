using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Autonomy.Domain;

public sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.MapStronglyTypedUuid<JobRun, JobRunId>(run => run.Id);
        builder.MapStronglyTypedLongId<JobRun, TenantId>(run => run.TenantId);
        builder.Property(run => run.PayloadJson).HasColumnType("jsonb");
    }
}

public sealed class TenantJobPolicyConfiguration : IEntityTypeConfiguration<TenantJobPolicy>
{
    public void Configure(EntityTypeBuilder<TenantJobPolicy> builder)
    {
        builder.MapStronglyTypedUuid<TenantJobPolicy, TenantJobPolicyId>(policy => policy.Id);
        builder.MapStronglyTypedLongId<TenantJobPolicy, TenantId>(policy => policy.TenantId);
    }
}

public interface IJobRunRepository : IAppendRepository<JobRun, JobRunId>
{
    /// <summary>True when a run already exists for the (job type, trigger) pair — the runner's idempotency check.</summary>
    Task<bool> ExistsForTriggerUnfilteredAsync(TenantId tenantId, string jobType, string triggerReference, CancellationToken cancellationToken);

    /// <summary>Counts runs executed today for the tenant (the daily autonomous-action cap, design §6.8).</summary>
    Task<int> CountExecutedSinceUnfilteredAsync(TenantId tenantId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>The tenant's runs, newest first — the "Handled by Nerova" feed and suggestion inbox.</summary>
    Task<JobRun[]> GetByTenantAsync(JobRunStatus? status, int limit, CancellationToken cancellationToken);

    void Update(JobRun jobRun);
}

public sealed class JobRunRepository(MainDbContext mainDbContext)
    : RepositoryBase<JobRun, JobRunId>(mainDbContext), IJobRunRepository
{
    public async Task<bool> ExistsForTriggerUnfilteredAsync(TenantId tenantId, string jobType, string triggerReference, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(run => run.TenantId == tenantId && run.JobType == jobType && run.TriggerReference == triggerReference, cancellationToken);
    }

    public async Task<int> CountExecutedSinceUnfilteredAsync(TenantId tenantId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var runs = await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(run => run.TenantId == tenantId && run.Status == JobRunStatus.Completed)
            .ToArrayAsync(cancellationToken);

        return runs.Count(run => run.ExecutedAt >= since);
    }

    public async Task<JobRun[]> GetByTenantAsync(JobRunStatus? status, int limit, CancellationToken cancellationToken)
    {
        var query = DbSet.AsQueryable();
        if (status is not null)
        {
            query = query.Where(run => run.Status == status);
        }

        var runs = await query.ToArrayAsync(cancellationToken);
        return [.. runs.OrderByDescending(run => run.CreatedAt).Take(limit)];
    }

    public new void Update(JobRun jobRun)
    {
        base.Update(jobRun);
    }
}

public interface ITenantJobPolicyRepository : IAppendRepository<TenantJobPolicy, TenantJobPolicyId>
{
    Task<TenantJobPolicy?> GetByJobTypeUnfilteredAsync(TenantId tenantId, string jobType, CancellationToken cancellationToken);

    Task<TenantJobPolicy[]> GetByTenantAsync(CancellationToken cancellationToken);

    void Update(TenantJobPolicy policy);
}

public sealed class TenantJobPolicyRepository(MainDbContext mainDbContext)
    : RepositoryBase<TenantJobPolicy, TenantJobPolicyId>(mainDbContext), ITenantJobPolicyRepository
{
    public async Task<TenantJobPolicy?> GetByJobTypeUnfilteredAsync(TenantId tenantId, string jobType, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(policy => policy.TenantId == tenantId && policy.JobType == jobType, cancellationToken);
    }

    public async Task<TenantJobPolicy[]> GetByTenantAsync(CancellationToken cancellationToken)
    {
        return await DbSet.ToArrayAsync(cancellationToken);
    }

    public new void Update(TenantJobPolicy policy)
    {
        base.Update(policy);
    }
}
