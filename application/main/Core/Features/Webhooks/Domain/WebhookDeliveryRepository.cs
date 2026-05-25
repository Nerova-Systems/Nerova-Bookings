using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Webhooks.Domain;

public interface IWebhookDeliveryRepository : ICrudRepository<WebhookDelivery, WebhookDeliveryId>
{
    /// <summary>
    ///     Returns up to <paramref name="batchSize" /> pending/failed deliveries whose
    ///     <see cref="WebhookDelivery.NextAttemptAt" /> is at or before <paramref name="now" />.
    ///     Ignores the tenant filter because the delivery worker is tenant-agnostic.
    /// </summary>
    Task<WebhookDelivery[]> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    Task<WebhookDelivery[]> GetForWebhookAsync(WebhookId webhookId, CancellationToken cancellationToken);
}

public sealed class WebhookDeliveryRepository(MainDbContext mainDbContext)
    : RepositoryBase<WebhookDelivery, WebhookDeliveryId>(mainDbContext), IWebhookDeliveryRepository
{
    public async Task<WebhookDelivery[]> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(delivery => delivery.Status == WebhookDeliveryStatus.Pending || delivery.Status == WebhookDeliveryStatus.Failed)
            .Where(delivery => delivery.NextAttemptAt != null && delivery.NextAttemptAt <= now)
            .OrderBy(delivery => delivery.NextAttemptAt)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<WebhookDelivery[]> GetForWebhookAsync(WebhookId webhookId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(delivery => delivery.WebhookId == webhookId)
            .OrderByDescending(delivery => delivery.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }
}
