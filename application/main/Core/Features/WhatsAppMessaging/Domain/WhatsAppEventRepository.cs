using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppMessaging.Domain;

public interface IWhatsAppEventRepository : IAppendRepository<WhatsAppEvent, WhatsAppEventId>
{
    /// <summary>
    ///     Returns true when an event with the given <paramref name="metaEventId" /> already exists.
    ///     Used by the webhook endpoint to deduplicate redeliveries without inserting a duplicate row.
    /// </summary>
    Task<bool> ExistsAsync(string metaEventId, CancellationToken cancellationToken);

    void Update(WhatsAppEvent whatsAppEvent);
}

public sealed class WhatsAppEventRepository(MainDbContext mainDbContext)
    : RepositoryBase<WhatsAppEvent, WhatsAppEventId>(mainDbContext), IWhatsAppEventRepository
{
    public async Task<bool> ExistsAsync(string metaEventId, CancellationToken cancellationToken)
    {
        return await DbSet.AnyAsync(e => e.MetaEventId == metaEventId, cancellationToken);
    }

    public new void Update(WhatsAppEvent whatsAppEvent)
    {
        base.Update(whatsAppEvent);
    }
}
