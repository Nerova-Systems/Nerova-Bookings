using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Persistence;

namespace Account.Features.Subscriptions.Domain;

public interface ISubscriptionRepository : ICrudRepository<Subscription, SubscriptionId>
{
    Task<Subscription> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves a subscription by tenant ID without applying tenant query filters.
    ///     This method is used when tenant context is not available (e.g., during signup token creation).
    /// </summary>
    Task<Subscription?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken);
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
}
