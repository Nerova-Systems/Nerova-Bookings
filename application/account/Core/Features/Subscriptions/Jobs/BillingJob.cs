using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Integrations.PayFast;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Jobs;

/// <summary>
///     Runs daily at 02:00 UTC. Charges active subscriptions due for renewal and applies scheduled plan changes.
/// </summary>
public sealed class BillingJob(IServiceScopeFactory scopeFactory, ILogger<BillingJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilNextRunAsync(hour: 2, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            logger.LogInformation("BillingJob starting");
            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var payFastClient = scope.ServiceProvider.GetRequiredService<IPayFastClient>();
        var events = scope.ServiceProvider.GetRequiredService<ITelemetryEventsCollector>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow();
        var dueSubscriptions = await subscriptionRepository.GetAllDueForBillingUnfilteredAsync(now, cancellationToken);

        logger.LogInformation("BillingJob found {Count} subscriptions due for billing", dueSubscriptions.Length);

        foreach (var subscription in dueSubscriptions)
        {
            await ChargeSubscriptionAsync(subscription, subscriptionRepository, tenantRepository, dbContext, payFastClient, events, now, cancellationToken);
        }
    }

    private async Task ChargeSubscriptionAsync(
        Subscription subscription,
        ISubscriptionRepository subscriptionRepository,
        ITenantRepository tenantRepository,
        AccountDbContext dbContext,
        IPayFastClient payFastClient,
        ITelemetryEventsCollector events,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await ApplySubscriptionChargeAsync(subscription, subscriptionRepository, tenantRepository, payFastClient, events, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException && attempt < 3)
            {
                logger.LogWarning(ex, "BillingJob concurrency conflict for subscription {SubscriptionId}. Retrying attempt {Attempt}.", subscription.Id, attempt);
                dbContext.ChangeTracker.Clear();
                var reloaded = await subscriptionRepository.GetByTenantIdUnfilteredAsync(subscription.TenantId, cancellationToken);
                if (reloaded is null || reloaded.NextBillingDate > now || reloaded.Status != SubscriptionStatus.Active)
                {
                    return;
                }

                subscription = reloaded;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BillingJob failed to charge subscription {SubscriptionId}", subscription.Id);
                return;
            }
        }
    }

    private static async Task ApplySubscriptionChargeAsync(
        Subscription subscription,
        ISubscriptionRepository subscriptionRepository,
        ITenantRepository tenantRepository,
        IPayFastClient payFastClient,
        ITelemetryEventsCollector events,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var pendingDowngrade = subscription.ScheduledPlan;
        var planToCharge = pendingDowngrade ?? subscription.Plan;
        var amount = SubscriptionPlanPricing.GetMonthlyPrice(planToCharge);

        var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken!, amount, $"Nerova Bookings {planToCharge} Plan - monthly billing", cancellationToken);

        if (charged)
        {
            var previousPlan = subscription.Plan;
            var transaction = new PaymentTransaction(
                PaymentTransactionId.NewId(),
                amount,
                SubscriptionPlanPricing.Currency,
                PaymentTransactionStatus.Succeeded,
                now,
                null,
                null,
                null,
                "PayFast",
                null,
                "billing-worker"
            );
            subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));
            subscription.RenewBillingPeriod(now);

            var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
            tenant?.UpdatePlan(subscription.Plan);
            if (tenant is not null)
            {
                tenantRepository.Update(tenant);
            }

            if (pendingDowngrade is not null && planToCharge != previousPlan)
            {
                var previousPrice = SubscriptionPlanPricing.GetMonthlyPrice(previousPlan);
                var daysOnPlan = subscription.CurrentPeriodStart.HasValue ? (int)(now - subscription.CurrentPeriodStart.Value).TotalDays : 30;
                events.CollectEvent(new SubscriptionDowngraded(subscription.Id, previousPlan, planToCharge, daysOnPlan, previousPrice, amount, amount - previousPrice, SubscriptionPlanPricing.Currency));
            }
            else
            {
                events.CollectEvent(new SubscriptionRenewed(subscription.Id, planToCharge, amount, amount, SubscriptionPlanPricing.Currency));
            }
        }
        else
        {
            var transaction = new PaymentTransaction(
                PaymentTransactionId.NewId(),
                amount,
                SubscriptionPlanPricing.Currency,
                PaymentTransactionStatus.Failed,
                now,
                "PayFast charge failed",
                null,
                null,
                "PayFast",
                null,
                "billing-worker"
            );
            subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));
            subscription.SetPastDue(now);
            events.CollectEvent(new PaymentFailed(subscription.Id, subscription.Plan, amount, SubscriptionPlanPricing.Currency));
        }

        subscriptionRepository.Update(subscription);
    }

    private static async Task WaitUntilNextRunAsync(int hour, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);

        var delay = nextRun - now;
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }
}
