using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Jobs;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class BillingDunningServiceTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ProcessPastDueSubscriptions_WhenGracePeriodExpired_ShouldSuspendTenant()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.PastDue)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("first_payment_failed_at", TimeProvider.System.GetUtcNow().AddDays(-8))
            ]
        );
        var service = Provider.GetRequiredService<BillingDunningService>();

        // Act
        await service.ProcessPastDueSubscriptionsAsync(CancellationToken.None);

        // Assert
        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Suspended));
        var suspensionReason = Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        suspensionReason.Should().Be(nameof(SuspensionReason.PaymentFailed));
    }

    [Fact]
    public async Task ProcessPastDueSubscriptions_WhenGracePeriodStillActive_ShouldKeepTenantActive()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.PastDue)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("first_payment_failed_at", TimeProvider.System.GetUtcNow().AddDays(-3))
            ]
        );
        var service = Provider.GetRequiredService<BillingDunningService>();

        // Act
        await service.ProcessPastDueSubscriptionsAsync(CancellationToken.None);

        // Assert
        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Active));
    }
}
