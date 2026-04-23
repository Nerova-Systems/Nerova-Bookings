using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Integrations.PayFast;
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
        var payFastClient = scope.ServiceProvider.GetRequiredService<IPayFastClient>();
        var events = scope.ServiceProvider.GetRequiredService<ITelemetryEventsCollector>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow();
        var dueSubscriptions = await subscriptionRepository.GetAllDueForBillingUnfilteredAsync(now, cancellationToken);

        logger.LogInformation("BillingJob found {Count} subscriptions due for billing", dueSubscriptions.Length);

        foreach (var subscription in dueSubscriptions)
        {
            await ChargeSubscriptionAsync(subscription, subscriptionRepository, payFastClient, events, now, cancellationToken);
        }
    }

    private async Task ChargeSubscriptionAsync(Subscription subscription, ISubscriptionRepository subscriptionRepository, IPayFastClient payFastClient, ITelemetryEventsCollector events, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            // Apply scheduled downgrade before charging (charge at the new rate)
            var pendingDowngrade = subscription.ScheduledPlan;
            var planToCharge = pendingDowngrade ?? subscription.Plan;
            var amount = SubscriptionPlanPricing.GetMonthlyPrice(planToCharge);

            var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken!, amount, $"Nerova Bookings {planToCharge} Plan — monthly billing", cancellationToken);

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
                    null
                );
                subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));
                subscription.RenewBillingPeriod(now);

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
                subscription.SetPastDue(now);
                events.CollectEvent(new PaymentFailed(subscription.Id, subscription.Plan, amount, SubscriptionPlanPricing.Currency));
            }

            subscriptionRepository.Update(subscription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BillingJob failed to charge subscription {SubscriptionId}", subscription.Id);
        }
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
