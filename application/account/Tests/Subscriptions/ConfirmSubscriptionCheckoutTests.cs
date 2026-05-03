using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Validation;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class ConfirmSubscriptionCheckoutTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ConfirmSubscriptionCheckout_WhenPaystackReferenceIsVerified_ShouldActivateSubscription()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_id", MockPaystackClient.MockCustomerId),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var command = new ConfirmSubscriptionCheckoutCommand(MockPaystackClient.MockReference);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-checkout", command);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string>(
            "SELECT plan FROM subscriptions WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]
        ).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>(
            "SELECT paystack_subscription_id FROM subscriptions WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]
        ).Should().Be(MockPaystackClient.MockSubscriptionId);
        Connection.ExecuteScalar<string>(
            "SELECT payment_method FROM subscriptions WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]
        ).Should().Contain("\"Last4\":\"4242\"");
        Connection.ExecuteScalar<string>(
            "SELECT payment_transactions FROM subscriptions WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]
        ).Should().Contain(nameof(PaymentTransactionStatus.Succeeded));
    }

    [Fact]
    public async Task ConfirmSubscriptionCheckout_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new ConfirmSubscriptionCheckoutCommand(MockPaystackClient.MockReference);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-checkout", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");
    }
}
