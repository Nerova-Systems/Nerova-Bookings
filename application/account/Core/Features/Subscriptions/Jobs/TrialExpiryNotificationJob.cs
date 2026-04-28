using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Integrations.Email;

namespace Account.Features.Subscriptions.Jobs;

/// <summary>
///     Runs daily at 08:00 UTC. Sends trial expiry reminder emails at T-7, T-3, and T-1 days.
/// </summary>
public sealed class TrialExpiryNotificationJob(IServiceScopeFactory scopeFactory, ILogger<TrialExpiryNotificationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilNextRunAsync(hour: 8, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            logger.LogInformation("TrialExpiryNotificationJob starting");
            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var emailQueue = scope.ServiceProvider.GetRequiredService<ITransactionalEmailQueue>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow();

        foreach (var daysUntilExpiry in new[] { 7, 3, 1 })
        {
            var windowStart = now.AddDays(daysUntilExpiry - 1);
            var windowEnd = now.AddDays(daysUntilExpiry);

            var expiringTrials = await subscriptionRepository.GetAllExpiringTrialsUnfilteredAsync(windowStart, windowEnd, cancellationToken);

            logger.LogInformation("TrialExpiryNotificationJob found {Count} trials expiring in {Days} day(s)", expiringTrials.Length, daysUntilExpiry);

            if (expiringTrials.Length == 0) continue;

            var tenantIds = expiringTrials.Select(s => s.TenantId).ToArray();
            var ownerEmailsByTenantId = await dbContext.Set<User>()
                .IgnoreQueryFilters()
                .Where(u => tenantIds.Contains(u.TenantId) && u.Role == UserRole.Owner && u.DeletedAt == null)
                .ToDictionaryAsync(u => u.TenantId, u => u.Email, cancellationToken);

            foreach (var subscription in expiringTrials)
            {
                if (!ownerEmailsByTenantId.TryGetValue(subscription.TenantId, out var ownerEmail)) continue;

                await SendTrialExpiryEmailAsync(subscription, daysUntilExpiry, ownerEmail, emailQueue, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendTrialExpiryEmailAsync(Subscription subscription, int daysUntilExpiry, string ownerEmail, ITransactionalEmailQueue emailQueue, CancellationToken cancellationToken)
    {
        try
        {
            var subject = daysUntilExpiry == 1
                ? "Your Nerova Bookings trial expires tomorrow"
                : $"Your Nerova Bookings trial expires in {daysUntilExpiry} days";

            await emailQueue.EnqueueAsync(
                ownerEmail,
                subject,
                $"Your free trial ends in {daysUntilExpiry} day(s). Subscribe now to keep access to all features.",
                TransactionalEmailTemplateKeys.TrialExpiry,
                subscription.Id.Value,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TrialExpiryNotificationJob failed to send email for subscription {SubscriptionId}", subscription.Id);
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
