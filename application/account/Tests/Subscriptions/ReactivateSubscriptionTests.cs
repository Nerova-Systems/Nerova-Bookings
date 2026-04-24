using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Queries;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class ReactivateSubscriptionTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ReactivateSubscription_WhenCancelledAndOwner_ShouldSucceed()
    {
        // Arrange — put subscription into Cancelled state
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Cancelled)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/subscriptions/reactivate", null);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
    }

    [Fact]
    public async Task ReactivateSubscription_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Cancelled)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsync("/api/account/subscriptions/reactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReactivateSubscription_WhenNotCancelled_ShouldReturnBadRequest()
    {
        // Arrange — default seeded subscription is Trial (not Cancelled)

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/subscriptions/reactivate", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription is not cancelled. Nothing to reactivate.");
    }

    [Fact]
    public async Task ReactivateSubscription_WhenActive_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/subscriptions/reactivate", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription is not cancelled. Nothing to reactivate.");
    }
}
