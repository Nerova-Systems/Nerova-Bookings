using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Webhooks.Domain;

public interface IWebhookRepository : ICrudRepository<Webhook, WebhookId>
{
    Task<Webhook[]> GetForTenantAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every <see cref="Webhook.Active" /> webhook for <paramref name="tenantId" /> that
    ///     subscribes to <paramref name="eventType" />. Used by the dispatcher to fan-out a single
    ///     business event into one <see cref="WebhookDelivery" /> per subscriber. Bypasses the
    ///     ambient tenant query filter so the caller can pass a tenant id explicitly (e.g. from a
    ///     background worker without an HTTP-scoped <c>IExecutionContext</c>).
    /// </summary>
    Task<Webhook[]> GetActiveSubscribersAsync(TenantId tenantId, WebhookEventType eventType, CancellationToken cancellationToken);
}

public sealed class WebhookRepository(MainDbContext mainDbContext)
    : RepositoryBase<Webhook, WebhookId>(mainDbContext), IWebhookRepository
{
    public async Task<Webhook[]> GetForTenantAsync(CancellationToken cancellationToken)
    {
        // Sort client-side: SQLite (used in tests) cannot ORDER BY DateTimeOffset. Per-tenant
        // result sets are small, so the cost of in-memory ordering is negligible.
        var webhooks = await DbSet.ToArrayAsync(cancellationToken);
        return webhooks.OrderByDescending(webhook => webhook.CreatedAt).ToArray();
    }

    public async Task<Webhook[]> GetActiveSubscribersAsync(TenantId tenantId, WebhookEventType eventType, CancellationToken cancellationToken)
    {
        // Query string filter against the JSON column is safe because event names are members of a
        // closed enum vocabulary (no user-supplied input ever reaches this string). The
        // post-filter in memory enforces the contract — Contains may match substrings on SQLite.
        var eventName = eventType.ToString();
        var candidates = await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(webhook => webhook.TenantId == tenantId)
            .Where(webhook => webhook.Active)
            .Where(webhook => webhook.EventSubscriptionsJson.Contains(eventName))
            .ToArrayAsync(cancellationToken);

        return candidates.Where(webhook => webhook.IsSubscribedTo(eventType)).ToArray();
    }
}
