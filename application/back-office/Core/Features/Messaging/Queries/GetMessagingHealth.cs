using Azure.Messaging.ServiceBus.Administration;
using BackOffice.Database;
using JetBrains.Annotations;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharedKernel.Catalog;
using SharedKernel.Cqrs;
using SharedKernel.Outbox;
using LegacyOutboxMessage = SharedKernel.Outbox.OutboxMessage;
using MassTransitOutboxMessage = MassTransit.EntityFrameworkCoreIntegration.OutboxMessage;

namespace BackOffice.Features.Messaging.Queries;

[PublicAPI]
public sealed record GetMessagingHealthQuery : IRequest<Result<MessagingHealthResponse>>;

[PublicAPI]
public sealed record MessagingHealthResponse(
    MessagingHealthStatus Status,
    DateTimeOffset CheckedAt,
    OutboxStoreHealth LegacyOutbox,
    OutboxStoreHealth MassTransitOutbox,
    InboxStoreHealth MassTransitInbox,
    BrokerHealth Broker
);

[PublicAPI]
public sealed record OutboxStoreHealth(
    string Source,
    MessagingHealthStatus Status,
    int PendingCount,
    int ScheduledCount,
    int LockedCount,
    int ProcessedCount,
    int DeadLetteredCount,
    DateTimeOffset? OldestPendingAt,
    long OldestPendingAgeSeconds
);

[PublicAPI]
public sealed record InboxStoreHealth(
    MessagingHealthStatus Status,
    int DuplicateDetectionRowCount,
    DateTimeOffset? LatestReceivedAt
);

[PublicAPI]
public sealed record BrokerHealth(
    MessagingHealthStatus Status,
    string Provider,
    string? Error,
    BrokerSubscriptionHealth[] Subscriptions
)
{
    public static BrokerHealth Healthy(BrokerSubscriptionHealth[] subscriptions)
    {
        return new BrokerHealth(MessagingHealthStatus.Healthy, "AzureServiceBus", null, subscriptions);
    }
}

[PublicAPI]
public sealed record BrokerSubscriptionHealth(
    string TopicName,
    string SubscriptionName,
    MessagingHealthStatus Status,
    long ActiveMessageCount,
    long DeadLetterMessageCount,
    long TransferDeadLetterMessageCount,
    long TotalMessageCount,
    string? Error
);

[PublicAPI]
public enum MessagingHealthStatus
{
    Healthy,
    Warning,
    Degraded,
    Unavailable
}

public interface IServiceBusHealthClient
{
    Task<BrokerHealth> GetBrokerHealthAsync(CancellationToken cancellationToken);
}

public sealed class GetMessagingHealthHandler(BackOfficeDbContext dbContext, TimeProvider timeProvider, IServiceBusHealthClient serviceBusHealthClient)
    : IRequestHandler<GetMessagingHealthQuery, Result<MessagingHealthResponse>>
{
    private static readonly TimeSpan PendingWarningThreshold = TimeSpan.FromMinutes(5);

    public async Task<Result<MessagingHealthResponse>> Handle(GetMessagingHealthQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var legacyOutbox = await GetLegacyOutboxHealthAsync(now, cancellationToken);
        var massTransitOutbox = await GetMassTransitOutboxHealthAsync(now, cancellationToken);
        var massTransitInbox = await GetMassTransitInboxHealthAsync(cancellationToken);
        var broker = await serviceBusHealthClient.GetBrokerHealthAsync(cancellationToken);
        var status = WorstStatus([legacyOutbox.Status, massTransitOutbox.Status, massTransitInbox.Status, broker.Status]);

        return new MessagingHealthResponse(status, now, legacyOutbox, massTransitOutbox, massTransitInbox, broker);
    }

    private async Task<OutboxStoreHealth> GetLegacyOutboxHealthAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages.ToArrayAsync(cancellationToken);
        var statusCounts = messages.GroupBy(m => m.GetStatus(now)).ToDictionary(g => g.Key, g => g.Count());
        var oldestPendingAt = messages
            .Where(m => m.GetStatus(now) == OutboxMessageStatus.Pending)
            .Select(m => (DateTimeOffset?)m.CreatedAt)
            .Min();
        var oldestPendingAge = GetAgeSeconds(now, oldestPendingAt);

        return new OutboxStoreHealth(
            "Legacy",
            GetStoreStatus(oldestPendingAge, statusCounts.GetValueOrDefault(OutboxMessageStatus.DeadLettered)),
            statusCounts.GetValueOrDefault(OutboxMessageStatus.Pending),
            statusCounts.GetValueOrDefault(OutboxMessageStatus.Scheduled),
            statusCounts.GetValueOrDefault(OutboxMessageStatus.Locked),
            statusCounts.GetValueOrDefault(OutboxMessageStatus.Processed),
            statusCounts.GetValueOrDefault(OutboxMessageStatus.DeadLettered),
            oldestPendingAt,
            oldestPendingAge
        );
    }

    private async Task<OutboxStoreHealth> GetMassTransitOutboxHealthAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var messages = await dbContext.Set<MassTransitOutboxMessage>().ToArrayAsync(cancellationToken);
        var outboxStates = await dbContext.Set<OutboxState>().ToDictionaryAsync(s => s.OutboxId, cancellationToken);

        var pendingMessages = messages
            .Where(m =>
                {
                    outboxStates.TryGetValue(m.OutboxId ?? Guid.Empty, out var state);
                    var enqueueAt = m.EnqueueTime is null ? ToUtcOffset(m.SentTime) : ToUtcOffset(m.EnqueueTime.Value);
                    return state?.Delivered is null && enqueueAt <= now;
                }
            )
            .ToArray();
        var scheduledCount = messages.Count(m =>
            {
                outboxStates.TryGetValue(m.OutboxId ?? Guid.Empty, out var state);
                var enqueueAt = m.EnqueueTime is null ? ToUtcOffset(m.SentTime) : ToUtcOffset(m.EnqueueTime.Value);
                return state?.Delivered is null && enqueueAt > now;
            }
        );
        var processedCount = outboxStates.Values.Count(s => s.Delivered is not null);
        var oldestPendingAt = pendingMessages
            .Select(m => (DateTimeOffset?)(m.EnqueueTime is null ? ToUtcOffset(m.SentTime) : ToUtcOffset(m.EnqueueTime.Value)))
            .Min();
        var oldestPendingAge = GetAgeSeconds(now, oldestPendingAt);

        return new OutboxStoreHealth(
            "MassTransit",
            GetStoreStatus(oldestPendingAge, 0),
            pendingMessages.Length,
            scheduledCount,
            0,
            processedCount,
            0,
            oldestPendingAt,
            oldestPendingAge
        );
    }

    private async Task<InboxStoreHealth> GetMassTransitInboxHealthAsync(CancellationToken cancellationToken)
    {
        var inboxRows = await dbContext.Set<InboxState>().ToArrayAsync(cancellationToken);
        var latestReceivedAt = inboxRows
            .Select(i => (DateTimeOffset?)ToUtcOffset(i.Received))
            .Max();

        return new InboxStoreHealth(MessagingHealthStatus.Healthy, inboxRows.Length, latestReceivedAt);
    }

    private static MessagingHealthStatus GetStoreStatus(long oldestPendingAgeSeconds, int deadLetteredCount)
    {
        if (deadLetteredCount > 0)
        {
            return MessagingHealthStatus.Degraded;
        }

        return oldestPendingAgeSeconds > PendingWarningThreshold.TotalSeconds ? MessagingHealthStatus.Warning : MessagingHealthStatus.Healthy;
    }

    private static long GetAgeSeconds(DateTimeOffset now, DateTimeOffset? createdAt)
    {
        if (createdAt is null)
        {
            return 0;
        }

        return Math.Max(0, Convert.ToInt64((now - createdAt.Value).TotalSeconds));
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static MessagingHealthStatus WorstStatus(IEnumerable<MessagingHealthStatus> statuses)
    {
        return statuses
            .OrderByDescending(status => status switch
                {
                    MessagingHealthStatus.Degraded => 3,
                    MessagingHealthStatus.Unavailable => 2,
                    MessagingHealthStatus.Warning => 1,
                    _ => 0
                }
            )
            .First();
    }
}

public sealed class ServiceBusHealthClient(IConfiguration configuration) : IServiceBusHealthClient
{
    public async Task<BrokerHealth> GetBrokerHealthAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("messaging");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new BrokerHealth(
                MessagingHealthStatus.Unavailable,
                "AzureServiceBus",
                "Connection string 'messaging' is not configured.",
                CatalogMessagingTopology.BackOfficeCatalogSubscriptions.Select(subscription =>
                        new BrokerSubscriptionHealth(
                            subscription.TopicName,
                            subscription.BackOfficeSubscriptionName,
                            MessagingHealthStatus.Unavailable,
                            0,
                            0,
                            0,
                            0,
                            "Connection string 'messaging' is not configured."
                        )
                    )
                    .ToArray()
            );
        }

        var client = new ServiceBusAdministrationClient(connectionString);
        var subscriptionHealth = new List<BrokerSubscriptionHealth>();

        foreach (var subscription in CatalogMessagingTopology.BackOfficeCatalogSubscriptions)
        {
            subscriptionHealth.Add(await GetSubscriptionHealthAsync(client, subscription, cancellationToken));
        }

        var brokerStatus = subscriptionHealth.Any(s => s.Status == MessagingHealthStatus.Degraded)
            ? MessagingHealthStatus.Degraded
            : MessagingHealthStatus.Healthy;

        return new BrokerHealth(
            brokerStatus,
            "AzureServiceBus",
            subscriptionHealth.FirstOrDefault(s => s.Error is not null)?.Error,
            subscriptionHealth.ToArray()
        );
    }

    private static async Task<BrokerSubscriptionHealth> GetSubscriptionHealthAsync(
        ServiceBusAdministrationClient client,
        CatalogSubscriptionTopology subscription,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await client.GetSubscriptionRuntimePropertiesAsync(
                subscription.TopicName,
                subscription.BackOfficeSubscriptionName,
                cancellationToken
            );
            var properties = response.Value;
            var status = properties.DeadLetterMessageCount > 0 || properties.TransferDeadLetterMessageCount > 0
                ? MessagingHealthStatus.Degraded
                : MessagingHealthStatus.Healthy;

            return new BrokerSubscriptionHealth(
                subscription.TopicName,
                subscription.BackOfficeSubscriptionName,
                status,
                properties.ActiveMessageCount,
                properties.DeadLetterMessageCount,
                properties.TransferDeadLetterMessageCount,
                properties.TotalMessageCount,
                null
            );
        }
        catch (Exception exception)
        {
            return new BrokerSubscriptionHealth(
                subscription.TopicName,
                subscription.BackOfficeSubscriptionName,
                MessagingHealthStatus.Degraded,
                0,
                0,
                0,
                0,
                exception.Message
            );
        }
    }
}
