using System.Net;
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
    public async Task GetCurrentSubscription_WhenOwner_ShouldReturnSubscription()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/current");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        result!.Plan.Should().Be(SubscriptionPlan.Trial);
        result.Status.Should().Be(SubscriptionStatus.Trial);
        result.TrialEndsAt.Should().BeAfter(DateTimeOffset.UtcNow);
        result.ScheduledPlan.Should().BeNull();
        result.CurrentPeriodEnd.Should().BeNull();
        result.NextBillingDate.Should().BeNull();
        result.CancelledAt.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentSubscription_WhenMember_ShouldAlsoReturnSubscription()
    {
        // Any authenticated user (including members) can view the current subscription
        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/account/subscriptions/current");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        result!.Status.Should().Be(SubscriptionStatus.Trial);
    }

    [Fact]
    public async Task GetCurrentSubscription_WhenActiveSubscription_ShouldReturnCorrectStatus()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/current");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        result!.Status.Should().Be(SubscriptionStatus.Active);
        result.Plan.Should().Be(SubscriptionPlan.Standard);
    }

    [Fact]
    public async Task GetCurrentSubscription_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/account/subscriptions/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
