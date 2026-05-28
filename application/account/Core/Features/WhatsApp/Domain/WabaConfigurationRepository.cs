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

    /// <summary>
    ///     Returns all WABA configurations that are fully linked to Meta — i.e. have both a
    ///     <see cref="WabaConfiguration.PhoneNumberId" /> and a
    ///     <see cref="WabaConfiguration.WabaAccessToken" />. Used by the Phase 7b drift detector
    ///     which scans cross-tenant once per day.
    /// </summary>
    Task<List<WabaConfiguration>> GetAllLinkedAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all WABA configurations whose <see cref="WabaConfiguration.DisplayNameStatus" />
    ///     is <see cref="WabaDisplayNameStatus.PendingReview" /> and which have a phone number id
    ///     and access token. Drives the Phase 7c display-name review poller.
    /// </summary>
    Task<List<WabaConfiguration>> GetAllPendingDisplayNameReviewAsync(CancellationToken cancellationToken);
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

    public Task<List<WabaConfiguration>> GetAllLinkedAsync(CancellationToken cancellationToken)
    {
        return DbSet
            .Where(w => w.PhoneNumberId != null && w.WabaAccessToken != null)
            .ToListAsync(cancellationToken);
    }

    public Task<List<WabaConfiguration>> GetAllPendingDisplayNameReviewAsync(CancellationToken cancellationToken)
    {
        return DbSet
            .Where(w => w.DisplayNameStatus == WabaDisplayNameStatus.PendingReview
                        && w.PhoneNumberId != null
                        && w.WabaAccessToken != null)
            .ToListAsync(cancellationToken);
    }
}
