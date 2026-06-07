using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppOnboarding.Domain;

public interface IWhatsAppBusinessAccountRepository : IAppendRepository<WhatsAppBusinessAccount, WhatsAppBusinessAccountId>
{
    /// <summary>
    ///     Retrieves the WhatsApp Business Account for the current tenant, or null when the tenant has not
    ///     onboarded. There is at most one WABA per tenant (enforced by a unique index on tenant_id), so the
    ///     tenant query filter guarantees a single row.
    /// </summary>
    Task<WhatsAppBusinessAccount?> GetByTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Looks up a WhatsApp Business Account by Meta phone number ID, bypassing the tenant query filter.
    ///     Used during webhook processing when no tenant context is available but we need to resolve
    ///     which tenant owns the incoming message.
    /// </summary>
    Task<WhatsAppBusinessAccount?> GetByMetaPhoneNumberIdUnfilteredAsync(string metaPhoneNumberId, CancellationToken cancellationToken);

    /// <summary>
    ///     Looks up a WhatsApp Business Account for a specific tenant, bypassing the tenant query filter.
    ///     Used by public/anonymous code paths (e.g. the public booking page) where no tenant context is
    ///     established but the tenant has already been resolved from the request.
    /// </summary>
    Task<WhatsAppBusinessAccount?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken);

    void Remove(WhatsAppBusinessAccount account);
}

public sealed class WhatsAppBusinessAccountRepository(MainDbContext mainDbContext)
    : RepositoryBase<WhatsAppBusinessAccount, WhatsAppBusinessAccountId>(mainDbContext), IWhatsAppBusinessAccountRepository
{
    public async Task<WhatsAppBusinessAccount?> GetByTenantAsync(CancellationToken cancellationToken)
    {
        return await DbSet.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<WhatsAppBusinessAccount?> GetByMetaPhoneNumberIdUnfilteredAsync(string metaPhoneNumberId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(a => a.PhoneNumber.MetaPhoneNumberId == metaPhoneNumberId, cancellationToken);
    }

    public async Task<WhatsAppBusinessAccount?> GetByTenantIdUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);
    }

    public new void Remove(WhatsAppBusinessAccount account)
    {
        base.Remove(account);
    }
}
