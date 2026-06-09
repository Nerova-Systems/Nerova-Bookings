using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppBooking.Domain;

public interface IWhatsAppLoginChallengeRepository : IAppendRepository<WhatsAppLoginChallenge, WhatsAppLoginChallengeId>
{
    /// <summary>
    ///     Finds the active challenge for a specific tenant and customer phone number.
    ///     Bypasses the tenant query filter because the caller may not have tenant context.
    /// </summary>
    Task<WhatsAppLoginChallenge?> GetByTenantAndPhoneUnfilteredAsync(TenantId tenantId, string phoneNumber, CancellationToken cancellationToken);

    void Update(WhatsAppLoginChallenge challenge);

    void Remove(WhatsAppLoginChallenge challenge);
}

public sealed class WhatsAppLoginChallengeRepository(MainDbContext mainDbContext)
    : RepositoryBase<WhatsAppLoginChallenge, WhatsAppLoginChallengeId>(mainDbContext), IWhatsAppLoginChallengeRepository
{
    public async Task<WhatsAppLoginChallenge?> GetByTenantAndPhoneUnfilteredAsync(TenantId tenantId, string phoneNumber, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PhoneNumber == phoneNumber, cancellationToken);
    }

    public new void Update(WhatsAppLoginChallenge challenge)
    {
        base.Update(challenge);
    }

    public new void Remove(WhatsAppLoginChallenge challenge)
    {
        base.Remove(challenge);
    }
}
