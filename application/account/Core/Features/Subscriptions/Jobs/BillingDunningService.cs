using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Jobs;

public sealed class BillingDunningService(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    AccountDbContext dbContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<BillingDunningService> logger
)
{
    public static readonly TimeSpan PaymentGracePeriod = TimeSpan.FromDays(7);

    public async Task ProcessPastDueSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.Subtract(PaymentGracePeriod);
        var subscriptions = await subscriptionRepository.GetAllPastDueForDunningUnfilteredAsync(cutoff, cancellationToken);

        foreach (var subscription in subscriptions)
        {
            var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
            if (tenant is null)
            {
                logger.LogWarning("Skipping billing suspension for missing tenant {TenantId}", subscription.TenantId);
                continue;
            }

            if (tenant.State == TenantState.Suspended && tenant.SuspensionReason == SuspensionReason.PaymentFailed)
            {
                continue;
            }

            tenant.Suspend(SuspensionReason.PaymentFailed, now);
            tenantRepository.Update(tenant);
            events.CollectEvent(new SubscriptionSuspended(subscription.Id, subscription.Plan, SuspensionReason.PaymentFailed, SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan), 0, SubscriptionPlanPricing.Currency));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
