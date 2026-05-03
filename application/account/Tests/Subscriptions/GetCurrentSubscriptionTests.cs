using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Queries;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class GetCurrentSubscriptionTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task GetCurrentSubscription_WhenExists_ShouldReturnSubscription()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_id", "CUS_test_123"),
                ("paystack_subscription_id", "SUB_test_123"),
                ("current_price_amount", 29.99),
                ("current_price_currency", "ZAR"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/current");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        result!.Plan.Should().Be(SubscriptionPlan.Standard);
        result.HasPaystackSubscription.Should().BeTrue();
        result.CancelAtPeriodEnd.Should().BeFalse();
        result.CurrentPriceAmount.Should().Be(29.99m);
        result.CurrentPriceCurrency.Should().Be("ZAR");
    }
}
