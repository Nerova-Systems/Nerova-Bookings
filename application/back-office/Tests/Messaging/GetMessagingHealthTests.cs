using System.Net;
using BackOffice.Database;
using BackOffice.Features.Messaging.Queries;
using FluentAssertions;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Outbox;
using SharedKernel.Tests;
using Xunit;
using LegacyOutboxMessage = SharedKernel.Outbox.OutboxMessage;
using MassTransitOutboxMessage = MassTransit.EntityFrameworkCoreIntegration.OutboxMessage;

namespace BackOffice.Tests.Messaging;

public sealed class GetMessagingHealthTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task GetMessagingHealth_WhenUserIsNotInternal_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/back-office/messaging/health");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMessagingHealth_WhenStoresAreEmpty_ShouldReturnZeroCountLocalStoreMetrics()
    {
        var response = await AuthenticatedSysOpHttpClient.GetAsync("/api/back-office/messaging/health");

        response.ShouldBeSuccessfulGetRequest();
        var health = await response.DeserializeResponse<MessagingHealthResponse>();
        health!.LegacyOutbox.PendingCount.Should().Be(0);
        health.LegacyOutbox.ScheduledCount.Should().Be(0);
        health.LegacyOutbox.LockedCount.Should().Be(0);
        health.LegacyOutbox.ProcessedCount.Should().Be(0);
        health.LegacyOutbox.DeadLetteredCount.Should().Be(0);
        health.MassTransitOutbox.PendingCount.Should().Be(0);
        health.MassTransitInbox.DuplicateDetectionRowCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMessagingHealth_WhenLegacyOutboxHasMessages_ShouldGroupByStatus()
    {
        var now = TimeProvider.GetUtcNow();
        var pending = LegacyOutboxMessage.Create(typeof(TestMessage).FullName!, """{"status":"pending"}""", now);
        var scheduled = LegacyOutboxMessage.Create(typeof(TestMessage).FullName!, """{"status":"scheduled"}""", now);
        scheduled.MarkFailed("retry later", now.AddMinutes(2));
        var locked = LegacyOutboxMessage.Create(typeof(TestMessage).FullName!, """{"status":"locked"}""", now);
        locked.Lock(now.AddMinutes(1));
        var processed = LegacyOutboxMessage.Create(typeof(TestMessage).FullName!, """{"status":"processed"}""", now);
        processed.MarkProcessed(now);
        var deadLettered = LegacyOutboxMessage.Create(typeof(TestMessage).FullName!, """{"status":"dead-lettered"}""", now);
        deadLettered.MarkDeadLettered("failed permanently", now);

        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
            dbContext.OutboxMessages.AddRange(pending, scheduled, locked, processed, deadLettered);
            await dbContext.SaveChangesAsync();
        }

        var response = await AuthenticatedSysOpHttpClient.GetAsync("/api/back-office/messaging/health");

        response.ShouldBeSuccessfulGetRequest();
        var health = await response.DeserializeResponse<MessagingHealthResponse>();
        health!.LegacyOutbox.PendingCount.Should().Be(1);
        health.LegacyOutbox.ScheduledCount.Should().Be(1);
        health.LegacyOutbox.LockedCount.Should().Be(1);
        health.LegacyOutbox.ProcessedCount.Should().Be(1);
        health.LegacyOutbox.DeadLetteredCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenMassTransitRowsExist_ShouldReportOutboxAndInboxMetrics()
    {
        var now = TimeProvider.GetUtcNow();
        var sentTime = now.AddMinutes(-10).UtcDateTime;

        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
        dbContext.Set<MassTransitOutboxMessage>().Add(
            new MassTransitOutboxMessage
            {
                MessageId = Guid.NewGuid(),
                MessageType = typeof(TestMessage).FullName!,
                ContentType = "application/json",
                Body = """{"name":"pending"}""",
                SentTime = sentTime
            }
        );
        dbContext.Set<InboxState>().Add(
            new InboxState
            {
                MessageId = Guid.NewGuid(),
                ConsumerId = Guid.NewGuid(),
                LockId = Guid.NewGuid(),
                Received = now.AddMinutes(-1).UtcDateTime,
                ReceiveCount = 1
            }
        );
        await dbContext.SaveChangesAsync();

        var handler = new GetMessagingHealthHandler(dbContext, TimeProvider, new FakeServiceBusHealthClient(BrokerHealth.Healthy([])));

        var result = await handler.Handle(new GetMessagingHealthQuery(), CancellationToken.None);

        result.Value.MassTransitOutbox.PendingCount.Should().Be(1);
        result.Value.MassTransitOutbox.OldestPendingAgeSeconds.Should().BeGreaterThanOrEqualTo(600);
        result.Value.MassTransitInbox.DuplicateDetectionRowCount.Should().Be(1);
        result.Value.MassTransitInbox.LatestReceivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenBrokerHasDeadLetters_ShouldReturnDegradedBroker()
    {
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
        var broker = new BrokerHealth(
            MessagingHealthStatus.Degraded,
            "AzureServiceBus",
            null,
            [
                new BrokerSubscriptionHealth(
                    "tenant-catalog-upserted",
                    "back-office-tenant-catalog-upserted",
                    MessagingHealthStatus.Degraded,
                    0,
                    3,
                    0,
                    3,
                    null
                )
            ]
        );
        var handler = new GetMessagingHealthHandler(dbContext, TimeProvider, new FakeServiceBusHealthClient(broker));

        var result = await handler.Handle(new GetMessagingHealthQuery(), CancellationToken.None);

        result.Value.Broker.Status.Should().Be(MessagingHealthStatus.Degraded);
        result.Value.Broker.Subscriptions.Should().ContainSingle(s => s.DeadLetterMessageCount == 3);
    }

    [Fact]
    public async Task Handle_WhenBrokerAdminFails_ShouldReturnDegradedBrokerWithoutFailing()
    {
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
        var broker = new BrokerHealth(
            MessagingHealthStatus.Degraded,
            "AzureServiceBus",
            "Unable to query Service Bus",
            [
                new BrokerSubscriptionHealth(
                    "tenant-catalog-upserted",
                    "back-office-tenant-catalog-upserted",
                    MessagingHealthStatus.Degraded,
                    0,
                    0,
                    0,
                    0,
                    "Unable to query Service Bus"
                )
            ]
        );
        var handler = new GetMessagingHealthHandler(dbContext, TimeProvider, new FakeServiceBusHealthClient(broker));

        var result = await handler.Handle(new GetMessagingHealthQuery(), CancellationToken.None);

        result.Value.Broker.Status.Should().Be(MessagingHealthStatus.Degraded);
        result.Value.Broker.Error.Should().Be("Unable to query Service Bus");
    }

    private sealed record TestMessage(string Name);

    private sealed class FakeServiceBusHealthClient(BrokerHealth brokerHealth) : IServiceBusHealthClient
    {
        public Task<BrokerHealth> GetBrokerHealthAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(brokerHealth);
        }
    }
}
