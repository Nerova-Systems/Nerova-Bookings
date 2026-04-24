using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class CancelSubscriptionTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task CancelSubscription_WhenActiveAndOwner_ShouldSucceed()
    {
        // Arrange — put subscription into Active state
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        var command = new CancelSubscriptionCommand(CancellationReason.TooExpensive, null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
    }

    [Fact]
    public async Task CancelSubscription_WhenPastDueAndOwner_ShouldSucceed()
    {
        // Arrange — put subscription into PastDue state
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.PastDue)),
                ("plan", nameof(SubscriptionPlan.Starter))
            ]
        );

        var command = new CancelSubscriptionCommand(CancellationReason.NoLongerNeeded, "No longer needed");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
    }

    [Fact]
    public async Task CancelSubscription_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        var command = new CancelSubscriptionCommand(CancellationReason.TooExpensive, null);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelSubscription_WhenTrialSubscription_ShouldReturnBadRequest()
    {
        // Arrange — default seeded subscription is Trial
        var command = new CancelSubscriptionCommand(CancellationReason.TooExpensive, null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Cannot cancel a subscription that is not active.");
    }

    [Fact]
    public async Task CancelSubscription_WhenAlreadyCancelled_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Cancelled)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        var command = new CancelSubscriptionCommand(CancellationReason.FoundAlternative, null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription is already cancelled.");
    }

    [Fact]
    public async Task CancelSubscription_WhenExpiredSubscription_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Expired)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        var command = new CancelSubscriptionCommand(CancellationReason.TooExpensive, null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Cannot cancel a subscription that is not active.");
    }

    [Fact]
    public async Task CancelSubscription_WhenFeedbackContainsHtml_ShouldReturnValidationError()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        var command = new CancelSubscriptionCommand(CancellationReason.Other, "<script>alert('xss')</script>");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
