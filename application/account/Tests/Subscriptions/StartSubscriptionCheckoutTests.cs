using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Validation;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class StartSubscriptionCheckoutTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task StartSubscriptionCheckout_WhenNoSavedPaymentMethod_ShouldReturnCheckoutSession()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", "CUS_test_123"),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var command = new StartSubscriptionCheckoutCommand(SubscriptionPlan.Standard);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StartSubscriptionCheckoutResponse>();
        result!.AccessCode.Should().NotBeNullOrEmpty();
        result.Reference.Should().NotBeNullOrEmpty();
        result.PublicKey.Should().NotBeNullOrEmpty();
        result.Amount.Should().BeGreaterThan(0);
        result.Currency.Should().NotBeNullOrEmpty();
        result.OperationPurpose.Should().Be("Subscribe");
        result.UsedExistingPaymentMethod.Should().BeFalse();

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionCheckoutStarted");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task StartSubscriptionCheckout_WhenSavedPaymentMethod_ShouldSubscribeDirectly()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", "CUS_test_123"),
                ("paystack_authorization_code", "AUTH_test_123"),
                ("paystack_authorization_email", "billing@example.com"),
                ("paystack_authorization_signature", "SIG_test_123"),
                ("payment_method", """{"Brand":"visa","Last4":"4242","ExpMonth":12,"ExpYear":2026}"""),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var command = new StartSubscriptionCheckoutCommand(SubscriptionPlan.Standard);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StartSubscriptionCheckoutResponse>();
        result!.AccessCode.Should().BeNull();
        result.Reference.Should().NotBeNullOrEmpty();
        result.OperationPurpose.Should().Be("Subscribe");
        result.UsedExistingPaymentMethod.Should().BeTrue();

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionCheckoutStarted");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task StartSubscriptionCheckout_WhenActiveSubscriptionExists_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "CUS_test_123"),
                ("paystack_authorization_code", "AUTH_test_123"),
                ("paystack_authorization_email", "billing@example.com"),
                ("paystack_authorization_signature", "SIG_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new StartSubscriptionCheckoutCommand(SubscriptionPlan.Premium);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "An active subscription already exists. Cannot create a new checkout session.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task StartSubscriptionCheckout_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new StartSubscriptionCheckoutCommand(SubscriptionPlan.Standard);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task StartSubscriptionCheckout_WhenBasisPlan_ShouldReturnValidationError()
    {
        // Arrange
        var command = new StartSubscriptionCheckoutCommand(SubscriptionPlan.Basis);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", command);

        // Assert
        var expectedErrors = new[]
        {
            new ErrorDetail("plan", "Cannot subscribe to the Basis plan.")
        };
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, expectedErrors);

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }
}
