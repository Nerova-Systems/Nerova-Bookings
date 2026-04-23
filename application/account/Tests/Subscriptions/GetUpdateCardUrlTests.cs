using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Queries;
using FluentAssertions;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class GetUpdateCardUrlTests : EndpointBaseTest<AccountDbContext>
{
    private const string TestToken = "test-payfast-token-card-update";

    [Fact]
    public async Task GetUpdateCardUrl_WhenActiveWithToken_ShouldReturnUpdateUrl()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken)
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/update-card-url");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<UpdateCardUrlResponse>();
        result!.Url.Should().Contain(TestToken);
        result.Url.Should().StartWith("https://");
    }

    [Fact]
    public async Task GetUpdateCardUrl_WhenNoToken_ShouldReturnBadRequest()
    {
        // Arrange — default seeded subscription has no token
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/subscriptions/update-card-url");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No payment method on file.");
    }

    [Fact]
    public async Task GetUpdateCardUrl_WhenNotOwner_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/account/subscriptions/update-card-url");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUpdateCardUrl_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/account/subscriptions/update-card-url");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
