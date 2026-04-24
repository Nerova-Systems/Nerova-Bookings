using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.PayFast;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class InitiateSubscriptionTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task InitiateSubscription_WhenTrialAndOwner_ShouldReturnUuid()
    {
        // Arrange
        var command = new InitiateSubscriptionCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/initiate", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var result = await response.Content.ReadFromJsonAsync<InitiateSubscriptionResponse>();
        result!.Uuid.Should().Be("test-uuid");
        await PayFastClient.Received(1).ProcessOnsitePaymentAsync(Arg.Any<SortedDictionary<string, string>>(), Arg.Any<CancellationToken>());
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionCheckoutStarted");
    }

    [Fact]
    public async Task InitiateSubscription_WhenCancelledSubscription_ShouldReturnUuid()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Cancelled)),
                ("plan", nameof(SubscriptionPlan.Starter))
            ]
        );
        var command = new InitiateSubscriptionCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/initiate", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var result = await response.Content.ReadFromJsonAsync<InitiateSubscriptionResponse>();
        result!.Uuid.Should().Be("test-uuid");
    }

    [Fact]
    public async Task InitiateSubscription_WhenActiveSubscription_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Starter))
            ]
        );
        var command = new InitiateSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/initiate", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription is already active. Use upgrade or downgrade instead.");
    }

    [Fact]
    public async Task InitiateSubscription_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new InitiateSubscriptionCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/initiate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InitiateSubscription_WhenTrialPlanRequested_ShouldReturnValidationError()
    {
        // Arrange
        var command = new InitiateSubscriptionCommand(SubscriptionPlan.Trial);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/initiate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InitiateSubscription_WhenPayFastFails_ShouldReturnBadRequest()
    {
        // Arrange
        PayFastClient.ProcessOnsitePaymentAsync(Arg.Any<SortedDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        var command = new InitiateSubscriptionCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/initiate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
