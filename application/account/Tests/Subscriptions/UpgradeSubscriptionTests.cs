using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class UpgradeSubscriptionTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task UpgradeSubscription_WhenStandardToPremium_ShouldSucceed()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("paystack_authorization_email", "billing@example.com"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30)),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Premium);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpgradeSubscriptionResponse>();
        result!.Reference.Should().NotBeNullOrEmpty();
        result.AccessCode.Should().BeNull();
        result.OperationPurpose.Should().Be("Upgrade");

        TelemetryEventsCollectorSpy.CollectedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task UpgradeSubscription_WhenPlanNotHigher_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Cannot upgrade from 'Premium' to 'Standard'. Target plan must be higher.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task UpgradeSubscription_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Premium);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }
}
