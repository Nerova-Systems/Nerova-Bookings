using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class UpgradeSubscriptionTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task UpgradeSubscription_WhenStandardToPremium_ShouldChargeProratedAmountAndUpgradeImmediately()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(SubscriptionPlan.Standard, 29.00m, now.AddDays(-15), now.AddDays(15));
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Premium);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpgradeSubscriptionResponse>();
        result!.AccessCode.Should().BeNull();
        result.Reference.Should().BeNull();
        result.Amount.Should().NotBeNull();
        result.Amount!.Value.Should().BeApproximately(35.00m, 0.01m);
        result.Currency.Should().Be(MockPaystackClient.MockStandardCurrency);
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Premium));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT current_price_amount FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(99.00m);
        Connection.ExecuteScalar<string>("SELECT plan FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Premium));
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE purpose = @purpose", [new { purpose = nameof(PaystackPaymentPurpose.Upgrade) }]).Should().Be(nameof(PaystackPaymentAttemptStatus.Succeeded));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT amount FROM paystack_payment_attempts WHERE purpose = @purpose", [new { purpose = nameof(PaystackPaymentPurpose.Upgrade) }]), CultureInfo.InvariantCulture).Should().BeApproximately(35.00m, 0.01m);
    }

    [Fact]
    public async Task UpgradeSubscription_WhenPlanNotHigher_ShouldReturnBadRequest()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(SubscriptionPlan.Premium, 99.00m, now.AddDays(-15), now.AddDays(15));
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Cannot upgrade from 'Premium' to 'Standard'. Target plan must be higher.");
    }

    [Fact]
    public async Task UpgradeSubscription_WhenNoPaystackAuthorization_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode)
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Premium);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No active Paystack authorization found.");
    }

    [Fact]
    public async Task UpgradeSubscription_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Premium);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");
    }

    private void SaveActiveSubscription(SubscriptionPlan plan, decimal currentPriceAmount, DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd)
    {
        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [
                ("state", nameof(TenantState.Active)),
                ("plan", plan.ToString())
            ]
        );

        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", plan.ToString()),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", MockPaystackClient.MockAuthorizationCode),
                ("paystack_authorization_email", "billing@example.com"),
                ("paystack_authorization_signature", "SIG_mock_12345"),
                ("current_price_amount", currentPriceAmount),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("current_period_start", currentPeriodStart),
                ("current_period_end", currentPeriodEnd),
                ("next_billing_at", currentPeriodEnd),
                ("payment_method", """{"Brand":"visa","Last4":"4242","ExpMonth":12,"ExpYear":2026}"""),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
    }
}
