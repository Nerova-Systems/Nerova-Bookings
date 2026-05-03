using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Account.Features.Subscriptions.Domain;

public interface IPaystackEventRepository : IAppendRepository<PaystackEvent, PaystackEventId>
{
    Task<bool> ExistsAsync(string paystackEventId, CancellationToken cancellationToken);

    void Update(PaystackEvent aggregate);

    Task<PaystackEvent[]> GetPendingByPaystackCustomerIdAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);

    /// <summary>
    ///     Checks if any pending events exist for a Paystack customer without locking.
    ///     Used by the frontend to poll for webhook processing completion.
    /// </summary>
    Task<bool> HasPendingByPaystackCustomerIdAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken);
}

internal sealed class PaystackEventRepository(AccountDbContext accountDbContext)
    : RepositoryBase<PaystackEvent, PaystackEventId>(accountDbContext), IPaystackEventRepository
{
    public async Task<bool> ExistsAsync(string paystackEventId, CancellationToken cancellationToken)
    {
        var id = PaystackEventId.NewId(paystackEventId);
        return await DbSet.AnyAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<PaystackEvent[]> GetPendingByPaystackCustomerIdAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(e => e.PaystackCustomerId == paystackCustomerId && e.Status == PaystackEventStatus.Pending)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> HasPendingByPaystackCustomerIdAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        return await DbSet.AnyAsync(e => e.PaystackCustomerId == paystackCustomerId && e.Status == PaystackEventStatus.Pending, cancellationToken);
    }
}
