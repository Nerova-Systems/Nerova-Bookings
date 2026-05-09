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
    ///     Retrieves a subscription by Paystack customer ID with pessimistic locking (FOR UPDATE).
    ///     This method should only be used in webhook processing to serialize with user-action commands.
    ///     This method bypasses tenant query filters since webhooks have no tenant context.
    /// </summary>
    Task<Subscription?> GetByPaystackCustomerIdWithLockUnfilteredAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves a subscription by tenant ID without applying tenant query filters.
    ///     This method is used when tenant context is not available (e.g., during signup token creation).
    /// </summary>
    Task<Subscription?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves due paid subscriptions without applying tenant query filters.
    ///     This method is used by the background billing lifecycle processor where tenant context is not established.
    /// </summary>
    Task<Subscription[]> GetDueForBillingUnfilteredAsync(DateTimeOffset dueAt, CancellationToken cancellationToken);
}

internal sealed class SubscriptionRepository(AccountDbContext accountDbContext, IExecutionContext executionContext)
    : RepositoryBase<Subscription, SubscriptionId>(accountDbContext), ISubscriptionRepository
{
    public async Task<Subscription> GetCurrentAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionContext.TenantId);
        return await DbSet.SingleAsync(s => s.TenantId == executionContext.TenantId, cancellationToken);
    }

    /// <summary>
    ///     Retrieves a subscription by Paystack customer ID with pessimistic locking (FOR UPDATE).
    ///     This method should only be used in webhook processing to serialize with user-action commands.
    ///     This method bypasses tenant query filters since webhooks have no tenant context.
    /// </summary>
    public async Task<Subscription?> GetByPaystackCustomerIdWithLockUnfilteredAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        if (accountDbContext.Database.ProviderName is "Microsoft.EntityFrameworkCore.Sqlite")
        {
            return await DbSet.IgnoreQueryFilters().SingleOrDefaultAsync(s => s.PaystackCustomerId == paystackCustomerId, cancellationToken);
        }

        return await DbSet
            .FromSqlInterpolated($"SELECT * FROM subscriptions WHERE paystack_customer_code = {paystackCustomerId.Value} FOR UPDATE")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Subscription?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.Local.SingleOrDefault(s => s.TenantId == tenantId)
               ?? await DbSet.IgnoreQueryFilters().SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
    }

    /// <summary>
    ///     Retrieves due paid subscriptions without applying tenant query filters.
    ///     This method is used by the background billing lifecycle processor where tenant context is not established.
    /// </summary>
    public async Task<Subscription[]> GetDueForBillingUnfilteredAsync(DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        return await DbSet
            .FromSqlInterpolated($"""
                                  SELECT *
                                  FROM subscriptions
                                  WHERE plan <> 'Basis'
                                    AND paystack_customer_code IS NOT NULL
                                    AND paystack_authorization_code IS NOT NULL
                                    AND next_billing_at IS NOT NULL
                                    AND next_billing_at <= {dueAt}
                                  ORDER BY id
                                  """
            )
            .IgnoreQueryFilters()
            .ToArrayAsync(cancellationToken);
    }
}
