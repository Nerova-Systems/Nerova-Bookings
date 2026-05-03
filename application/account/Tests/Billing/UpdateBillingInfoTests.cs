using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Billing.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Validation;
using Xunit;

namespace Account.Tests.Billing;

public sealed class UpdateBillingInfoTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task UpdateBillingInfo_WhenValid_ShouldSucceed()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_id", "CUS_test_123"),
                ("paystack_subscription_id", "SUB_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new UpdateBillingInfoCommand("Test Organization", "Vestergade 12", "1456", "Copenhagen", null, "DK", "billing@example.com", null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task UpdateBillingInfo_WhenMultiLineAddress_ShouldSplitIntoLine1AndLine2()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_id", "CUS_test_123"),
                ("paystack_subscription_id", "SUB_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new UpdateBillingInfoCommand("Test Organization", "Vestergade 12\nFloor 3", "1456", "Copenhagen", null, "DK", "billing@example.com", null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task UpdateBillingInfo_WhenNoPaystackCustomer_ShouldCreateCustomerAndSucceed()
    {
        // Arrange
        var command = new UpdateBillingInfoCommand("Test Organization", "Vestergade 12", "1456", "Copenhagen", null, "DK", "billing@example.com", null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task UpdateBillingInfo_WhenPaystackReusesCustomerForAnotherTenant_ShouldSucceed()
    {
        // Arrange
        var otherTenantId = TenantId.NewId();
        Connection.Insert("tenants", [
                ("id", otherTenantId.Value),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("name", "Other Tenant"),
                ("state", nameof(TenantState.Active)),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("logo", """{"Url":null,"Version":0}"""),
                ("suspension_reason", null),
                ("suspended_at", null)
            ]
        );
        Connection.Insert("subscriptions", [
                ("tenant_id", otherTenantId.Value),
                ("id", SubscriptionId.NewId().ToString()),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("scheduled_plan", null),
                ("paystack_customer_id", MockPaystackClient.MockCustomerId),
                ("paystack_subscription_id", null),
                ("current_price_amount", null),
                ("current_price_currency", null),
                ("current_period_end", null),
                ("cancel_at_period_end", false),
                ("first_payment_failed_at", null),
                ("cancellation_reason", null),
                ("cancellation_feedback", null),
                ("payment_transactions", "[]"),
                ("payment_method", null),
                ("billing_info", null)
            ]
        );
        var command = new UpdateBillingInfoCommand("Test Organization", "Vestergade 12", "1456", "Copenhagen", null, "DK", "billing@example.com", null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
        Connection.ExecuteScalar<string>(
                "SELECT paystack_customer_id FROM subscriptions WHERE tenant_id = @TenantId",
                [new { TenantId = DatabaseSeeder.Tenant1.Id.Value }]
            )
            .Should()
            .Be(MockPaystackClient.MockCustomerId);
    }

    [Fact]
    public async Task UpdateBillingInfo_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_id", "CUS_test_123"),
                ("paystack_subscription_id", "SUB_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new UpdateBillingInfoCommand("Test Organization", "Vestergade 12", "1456", "Copenhagen", null, "DK", "billing@example.com", null);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedMemberHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage billing information.");
    }

    [Fact]
    public async Task UpdateBillingInfo_WhenInvalidTaxId_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_id", "CUS_test_123"),
                ("paystack_subscription_id", "SUB_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new UpdateBillingInfoCommand("Test Organization", "Vestergade 12", "1456", "Copenhagen", null, "DK", "billing@example.com", "INVALID");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        var expectedErrors = new[] { new ErrorDetail("TaxId", "The provided Tax ID is not valid.") };
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, expectedErrors);
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(0);
    }

    [Fact]
    public async Task UpdateBillingInfo_WhenRequiredFieldsEmpty_ShouldReturnValidationErrors()
    {
        // Arrange
        var command = new UpdateBillingInfoCommand("", "", "", "", null, "", "", null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/billing/billing-info", command);

        // Assert
        var expectedErrors = new[]
        {
            new ErrorDetail("name", "Name must be between 1 and 100 characters."),
            new ErrorDetail("address", "Address must be between 1 and 200 characters."),
            new ErrorDetail("postalCode", "Postal code must be between 1 and 10 characters."),
            new ErrorDetail("city", "City must be between 1 and 50 characters."),
            new ErrorDetail("country", "Country is required."),
            new ErrorDetail("email", "Email must be in a valid format and no longer than 100 characters.")
        };
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, expectedErrors);
    }
}
