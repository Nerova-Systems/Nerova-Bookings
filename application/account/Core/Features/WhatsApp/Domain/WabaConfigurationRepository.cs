using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.WhatsApp.Domain;

public interface IWabaConfigurationRepository : ICrudRepository<WabaConfiguration, WabaConfigurationId>
{
    Task<WabaConfiguration?> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<WabaConfiguration?> GetByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken);
}

public sealed class WabaConfigurationRepository(AccountDbContext dbContext)
    : RepositoryBase<WabaConfiguration, WabaConfigurationId>(dbContext), IWabaConfigurationRepository
{
    public Task<WabaConfiguration?> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(w => w.TenantId == tenantId, cancellationToken);
    }

    public Task<WabaConfiguration?> GetByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(w => w.PhoneNumberId == phoneNumberId, cancellationToken);
    }
}
