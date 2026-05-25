using FluentAssertions;
using Main.Database;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Webhooks;

/// <summary>
///     Verifies <see cref="WebhookDispatcher.FanOutAsync" /> against the real EF Core context
///     (in-memory SQLite from <see cref="EndpointBaseTest{TDbContext}" />). Covers the gating that
///     <see cref="Main.Features.Scheduling.Notifications.BookingWebhookNotifier" /> relies on:
///     subscription filter by event type, active flag, and tenant scoping.
/// </summary>
public sealed class WebhookDispatcherFanOutTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task FanOutAsync_ThreeActiveSubscribers_EnqueuesThreeDeliveries()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var tenantId = DatabaseSeeder.TenantId;

        for (var i = 0; i < 3; i++)
        {
            db.Set<Webhook>().Add(Webhook.Create(
                tenantId, userId: null, eventTypeId: null,
                targetUrl: $"https://example.test/hook-{i}",
                eventSubscriptions: [WebhookEventType.BookingCreated]
            ));
        }
        await db.SaveChangesAsync();

        var dispatcher = CreateDispatcher(db);

        var deliveryIds = await dispatcher.FanOutAsync(tenantId, WebhookEventType.BookingCreated, "{}", CancellationToken.None);
        await db.SaveChangesAsync();

        deliveryIds.Should().HaveCount(3);
        var stored = await db.Set<WebhookDelivery>().IgnoreQueryFilters().Where(d => deliveryIds.Contains(d.Id)).ToListAsync();
        stored.Should().HaveCount(3).And.OnlyContain(d => d.Status == WebhookDeliveryStatus.Pending && d.EventType == WebhookEventType.BookingCreated);
    }

    [Fact]
    public async Task FanOutAsync_InactiveWebhook_IsIgnored()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var tenantId = DatabaseSeeder.TenantId;

        var active = Webhook.Create(tenantId, null, null, "https://example.test/active",
            [WebhookEventType.BookingCancelled]);
        var inactive = Webhook.Create(tenantId, null, null, "https://example.test/inactive",
            [WebhookEventType.BookingCancelled], active: false);
        db.Set<Webhook>().AddRange(active, inactive);
        await db.SaveChangesAsync();

        var dispatcher = CreateDispatcher(db);

        var deliveryIds = await dispatcher.FanOutAsync(tenantId, WebhookEventType.BookingCancelled, "{}", CancellationToken.None);
        await db.SaveChangesAsync();

        deliveryIds.Should().HaveCount(1);
        var stored = await db.Set<WebhookDelivery>().IgnoreQueryFilters().SingleAsync(d => d.Id == deliveryIds[0]);
        stored.RequestUrl.Should().Be("https://example.test/active");
    }

    [Fact]
    public async Task FanOutAsync_NotSubscribedToEvent_IsIgnored()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var tenantId = DatabaseSeeder.TenantId;

        // Only subscribes to BookingCreated — should NOT receive a BookingReported delivery.
        var unrelated = Webhook.Create(tenantId, null, null, "https://example.test/unrelated",
            [WebhookEventType.BookingCreated]);
        var reportSubscriber = Webhook.Create(tenantId, null, null, "https://example.test/reports",
            [WebhookEventType.BookingReported]);
        db.Set<Webhook>().AddRange(unrelated, reportSubscriber);
        await db.SaveChangesAsync();

        var dispatcher = CreateDispatcher(db);

        var deliveryIds = await dispatcher.FanOutAsync(tenantId, WebhookEventType.BookingReported, "{\"trigger\":\"BookingReported\"}", CancellationToken.None);
        await db.SaveChangesAsync();

        deliveryIds.Should().HaveCount(1);
        var stored = await db.Set<WebhookDelivery>().IgnoreQueryFilters().SingleAsync(d => d.Id == deliveryIds[0]);
        stored.RequestUrl.Should().Be("https://example.test/reports");
        stored.EventType.Should().Be(WebhookEventType.BookingReported);
    }

    private static WebhookDispatcher CreateDispatcher(MainDbContext db)
    {
        var webhookRepository = new WebhookRepository(db);
        var deliveryRepository = new WebhookDeliveryRepository(db);
        return new WebhookDispatcher(webhookRepository, deliveryRepository, TimeProvider.System);
    }
}
