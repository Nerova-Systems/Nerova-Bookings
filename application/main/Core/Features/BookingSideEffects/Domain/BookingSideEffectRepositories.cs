using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.BookingSideEffects.Domain;

public interface IWorkflowRepository : ICrudRepository<Workflow, WorkflowId>
{
    Task<Workflow[]> GetForEventTypeAsync(TenantId tenantId, UserId ownerUserId, EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<Workflow[]> GetActiveForEventTypeTriggerAsync(TenantId tenantId, EventTypeId eventTypeId, string trigger, CancellationToken cancellationToken);
}

public sealed class WorkflowRepository(MainDbContext mainDbContext)
    : RepositoryBase<Workflow, WorkflowId>(mainDbContext), IWorkflowRepository
{
    public async Task<Workflow[]> GetForEventTypeAsync(TenantId tenantId, UserId ownerUserId, EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(workflow => workflow.TenantId == tenantId)
            .Where(workflow => workflow.OwnerUserId == ownerUserId)
            .Where(workflow => workflow.EventTypeId == eventTypeId)
            .OrderBy(workflow => workflow.Name)
            .ThenBy(workflow => workflow.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Workflow[]> GetActiveForEventTypeTriggerAsync(TenantId tenantId, EventTypeId eventTypeId, string trigger, CancellationToken cancellationToken)
    {
        var normalizedTrigger = trigger.Trim().ToUpperInvariant();
        return await DbSet
            .IgnoreQueryFilters()
            .Where(workflow => workflow.TenantId == tenantId)
            .Where(workflow => workflow.EventTypeId == eventTypeId)
            .Where(workflow => workflow.Active)
            .Where(workflow => workflow.Trigger == normalizedTrigger)
            .ToArrayAsync(cancellationToken);
    }
}

public interface IWebhookSubscriptionRepository : ICrudRepository<WebhookSubscription, WebhookSubscriptionId>
{
    Task<WebhookSubscription[]> GetForEventTypeAsync(TenantId tenantId, UserId ownerUserId, EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<WebhookSubscription[]> GetActiveForEventTypeTriggerAsync(TenantId tenantId, EventTypeId eventTypeId, string trigger, CancellationToken cancellationToken);
}

public sealed class WebhookSubscriptionRepository(MainDbContext mainDbContext)
    : RepositoryBase<WebhookSubscription, WebhookSubscriptionId>(mainDbContext), IWebhookSubscriptionRepository
{
    public async Task<WebhookSubscription[]> GetForEventTypeAsync(TenantId tenantId, UserId ownerUserId, EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(subscription => subscription.TenantId == tenantId)
            .Where(subscription => subscription.OwnerUserId == ownerUserId)
            .Where(subscription => subscription.EventTypeId == eventTypeId)
            .OrderBy(subscription => subscription.SubscriberUrl)
            .ThenBy(subscription => subscription.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<WebhookSubscription[]> GetActiveForEventTypeTriggerAsync(TenantId tenantId, EventTypeId eventTypeId, string trigger, CancellationToken cancellationToken)
    {
        var normalizedTrigger = trigger.Trim().ToUpperInvariant();
        var subscriptions = await DbSet
            .IgnoreQueryFilters()
            .Where(subscription => subscription.TenantId == tenantId)
            .Where(subscription => subscription.EventTypeId == eventTypeId)
            .Where(subscription => subscription.Active)
            .ToArrayAsync(cancellationToken);

        return subscriptions
            .Where(subscription => subscription.Triggers.Contains(normalizedTrigger, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }
}

public interface IBookingSideEffectDeliveryRepository : IAppendRepository<BookingSideEffectDelivery, BookingSideEffectDeliveryId>
{
    void Update(BookingSideEffectDelivery delivery);

    Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken);

    Task<BookingSideEffectDelivery[]> GetForEventTypeAsync(TenantId tenantId, EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<BookingSideEffectDelivery[]> GetForBookingAsync(TenantId tenantId, BookingId bookingId, CancellationToken cancellationToken);
}

public sealed class BookingSideEffectDeliveryRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingSideEffectDelivery, BookingSideEffectDeliveryId>(mainDbContext), IBookingSideEffectDeliveryRepository
{
    public async Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken)
    {
        return await DbSet.IgnoreQueryFilters().AnyAsync(delivery => delivery.DedupeKey == dedupeKey, cancellationToken);
    }

    public async Task<BookingSideEffectDelivery[]> GetForEventTypeAsync(TenantId tenantId, EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        var deliveries = await DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(delivery => delivery.TenantId == tenantId)
            .Where(delivery => delivery.EventTypeId == eventTypeId)
            .ToArrayAsync(cancellationToken);

        return deliveries
            .OrderByDescending(delivery => delivery.NextRetryAt)
            .ThenByDescending(delivery => delivery.Id)
            .ToArray();
    }

    public async Task<BookingSideEffectDelivery[]> GetForBookingAsync(TenantId tenantId, BookingId bookingId, CancellationToken cancellationToken)
    {
        var deliveries = await DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(delivery => delivery.TenantId == tenantId)
            .Where(delivery => delivery.BookingId == bookingId)
            .ToArrayAsync(cancellationToken);

        return deliveries
            .OrderByDescending(delivery => delivery.NextRetryAt)
            .ThenByDescending(delivery => delivery.Id)
            .ToArray();
    }
}
