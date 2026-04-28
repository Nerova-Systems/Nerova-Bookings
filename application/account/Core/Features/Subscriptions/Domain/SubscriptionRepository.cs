using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.ExecutionContext;
using SharedKernel.Persistence;

namespace Account.Features.Subscriptions.Domain;

public interface ISubscriptionRepository : ICrudRepository<Subscription, SubscriptionId>
{
    Task<Subscription> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves a subscription by tenant ID without applying tenant query filters.
    ///     This method is used when tenant context is not available (e.g., during signup token creation or ITN handling).
    /// </summary>
    Task<Subscription?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<Subscription?> GetByPaymentTransactionIdUnfilteredAsync(PaymentTransactionId transactionId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all active subscriptions due for billing across all tenants.
    ///     WARNING: Disables tenant filter — must only be called from the billing background job.
    /// </summary>
    Task<Subscription[]> GetAllDueForBillingUnfilteredAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all past-due subscriptions whose payment grace period has expired.
    ///     WARNING: Disables tenant filter — must only be called from billing dunning processing.
    /// </summary>
    Task<Subscription[]> GetAllPastDueForDunningUnfilteredAsync(DateTimeOffset failedBefore, CancellationToken cancellationToken);

    Task<Subscription[]> GetAllForReconciliationUnfilteredAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all trial subscriptions with TrialEndsAt between 'from' and 'to' across all tenants.
    ///     WARNING: Disables tenant filter — must only be called from the trial expiry notification job.
    /// </summary>
    Task<Subscription[]> GetAllExpiringTrialsUnfilteredAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

internal sealed class SubscriptionRepository(AccountDbContext accountDbContext, IExecutionContext executionContext)
    : RepositoryBase<Subscription, SubscriptionId>(accountDbContext), ISubscriptionRepository
{
    public async Task<Subscription> GetCurrentAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionContext.TenantId);
        return await DbSet.SingleAsync(s => s.TenantId == executionContext.TenantId, cancellationToken);
    }

    public async Task<Subscription?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.Local.SingleOrDefault(s => s.TenantId == tenantId)
               ?? await DbSet.IgnoreQueryFilters().SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
    }

    public async Task<Subscription?> GetByPaymentTransactionIdUnfilteredAsync(PaymentTransactionId transactionId, CancellationToken cancellationToken)
    {
        var subscriptions = await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .ToArrayAsync(cancellationToken);

        return subscriptions.SingleOrDefault(s => s.PaymentTransactions.Any(t => t.Id == transactionId));
    }

    public async Task<Subscription[]> GetAllDueForBillingUnfilteredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(s => s.Status == SubscriptionStatus.Active && s.NextBillingDate <= now && s.PayFastToken != null)
            .OrderBy(s => s.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Subscription[]> GetAllPastDueForDunningUnfilteredAsync(DateTimeOffset failedBefore, CancellationToken cancellationToken)
    {
        var pastDueSubscriptions = await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.FirstPaymentFailedAt != null)
            .OrderBy(s => s.Id)
            .ToArrayAsync(cancellationToken);

        return pastDueSubscriptions
            .Where(s => s.FirstPaymentFailedAt <= failedBefore)
            .ToArray();
    }

    public async Task<Subscription[]> GetAllForReconciliationUnfilteredAsync(CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(s => s.PayFastToken != null || s.Status == SubscriptionStatus.PastDue || s.Status == SubscriptionStatus.Cancelled)
            .OrderBy(s => s.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Subscription[]> GetAllExpiringTrialsUnfilteredAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(s => s.Status == SubscriptionStatus.Trial && s.TrialEndsAt >= from && s.TrialEndsAt < to)
            .OrderBy(s => s.Id)
            .ToArrayAsync(cancellationToken);
    }
}
