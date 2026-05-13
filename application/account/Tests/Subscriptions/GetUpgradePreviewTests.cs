using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Queries;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class GetUpgradePreviewTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task GetUpgradePreview_WhenStandardToPremium_ShouldReturnProratedAmount()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(SubscriptionPlan.Standard, 29.00m, now.AddDays(-15), now.AddDays(15));

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/upgrade-preview?NewPlan=Premium");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpgradePreviewResponse>();
        result!.TotalAmount.Should().BeApproximately(35.00m, 0.01m);
        result.Currency.Should().Be(MockPaystackClient.MockStandardCurrency);
        result.LineItems.Should().Contain(i => i.Description == "Premium prorated upgrade" && i.IsProration);
        result.LineItems.Should().Contain(i => i.Description == "Tax" && i.IsTax);
    }

    [Fact]
    public async Task GetUpgradePreview_WhenPlanNotHigher_ShouldReturnBadRequest()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(SubscriptionPlan.Premium, 99.00m, now.AddDays(-15), now.AddDays(15));

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/upgrade-preview?NewPlan=Standard");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Cannot upgrade from 'Premium' to 'Standard'. Target plan must be higher.");
    }

    [Fact]
    public async Task GetUpgradePreview_WhenNoPaystackAuthorization_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode)
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/upgrade-preview?NewPlan=Premium");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No active Paystack authorization found.");
    }

    private void SaveActiveSubscription(SubscriptionPlan plan, decimal currentPriceAmount, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd)
    {
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", plan.ToString()),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", MockPaystackClient.MockAuthorizationCode),
                ("paystack_authorization_email", "billing@example.com"),
                ("current_price_amount", currentPriceAmount),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("current_period_start", currentPeriodStart),
                ("current_period_end", currentPeriodEnd),
                ("next_billing_at", currentPeriodEnd)
            ]
        );
    }
}
