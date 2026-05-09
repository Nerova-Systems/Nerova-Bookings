using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Account.Features.Subscriptions.Domain;

public interface IPaystackPaymentAttemptRepository : ICrudRepository<PaystackPaymentAttempt, PaystackPaymentAttemptId>
{
    Task<PaystackPaymentAttempt?> GetByReferenceAsync(string paystackReference, CancellationToken cancellationToken);

    Task<PaystackPaymentAttempt?> GetByReferenceUnfilteredAsync(string paystackReference, CancellationToken cancellationToken);

    Task<PaystackPaymentAttempt?> GetByReferenceWithLockUnfilteredAsync(string paystackReference, CancellationToken cancellationToken);
}

internal sealed class PaystackPaymentAttemptRepository(AccountDbContext accountDbContext)
    : RepositoryBase<PaystackPaymentAttempt, PaystackPaymentAttemptId>(accountDbContext), IPaystackPaymentAttemptRepository
{
    public async Task<PaystackPaymentAttempt?> GetByReferenceAsync(string paystackReference, CancellationToken cancellationToken)
    {
        return DbSet.Local.SingleOrDefault(a => a.PaystackReference == paystackReference)
               ?? await DbSet.SingleOrDefaultAsync(a => a.PaystackReference == paystackReference, cancellationToken);
    }

    public async Task<PaystackPaymentAttempt?> GetByReferenceUnfilteredAsync(string paystackReference, CancellationToken cancellationToken)
    {
        return DbSet.Local.SingleOrDefault(a => a.PaystackReference == paystackReference)
               ?? await DbSet.IgnoreQueryFilters().SingleOrDefaultAsync(a => a.PaystackReference == paystackReference, cancellationToken);
    }

    public async Task<PaystackPaymentAttempt?> GetByReferenceWithLockUnfilteredAsync(string paystackReference, CancellationToken cancellationToken)
    {
        if (accountDbContext.Database.ProviderName is "Microsoft.EntityFrameworkCore.Sqlite")
        {
            return await GetByReferenceUnfilteredAsync(paystackReference, cancellationToken);
        }

        return await DbSet
            .FromSqlInterpolated($"SELECT * FROM paystack_payment_attempts WHERE paystack_reference = {paystackReference} FOR UPDATE")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
