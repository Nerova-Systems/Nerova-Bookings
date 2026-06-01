using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppMessaging.Domain;

public interface IWhatsAppMessageRepository : IAppendRepository<WhatsAppMessage, WhatsAppMessageId>
{
    /// <summary>
    ///     Returns all messages for the current tenant ordered by timestamp descending.
    /// </summary>
    Task<WhatsAppMessage[]> GetByTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Looks up a message by Meta message ID, bypassing the tenant query filter.
    ///     Used during webhook status-update processing when no tenant context is available.
    /// </summary>
    Task<WhatsAppMessage?> GetByMetaMessageIdUnfilteredAsync(string metaMessageId, CancellationToken cancellationToken);

    void Update(WhatsAppMessage message);
}

public sealed class WhatsAppMessageRepository(MainDbContext mainDbContext)
    : RepositoryBase<WhatsAppMessage, WhatsAppMessageId>(mainDbContext), IWhatsAppMessageRepository
{
    public async Task<WhatsAppMessage[]> GetByTenantAsync(CancellationToken cancellationToken)
    {
        var messages = await DbSet.ToListAsync(cancellationToken);
        return [.. messages.OrderByDescending(m => m.Timestamp)];
    }

    public async Task<WhatsAppMessage?> GetByMetaMessageIdUnfilteredAsync(string metaMessageId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(m => m.MetaMessageId == metaMessageId, cancellationToken);
    }

    public new void Update(WhatsAppMessage message)
    {
        base.Update(message);
    }
}
