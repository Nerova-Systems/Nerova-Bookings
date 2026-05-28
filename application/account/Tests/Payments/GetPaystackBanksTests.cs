using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Payments.Queries;
using FluentAssertions;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Payments;

public sealed class GetPaystackBanksTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task GetBanks_WhenAuthenticated_ShouldReturnBanks()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/payments/banks");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetPaystackBanksResponse>();
        result.Should().NotBeNull();
        result!.Banks.Should().NotBeEmpty();
        result.Banks.Should().AllSatisfy(b =>
            {
                b.Code.Should().NotBeNullOrEmpty();
                b.Name.Should().NotBeNullOrEmpty();
            }
        );
    }

    [Fact]
    public async Task GetBanks_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/payments/banks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBanks_ShouldIncludeAccessBank()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/payments/banks");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<GetPaystackBanksResponse>();
        result!.Banks.Should().ContainSingle(b => b.Code == "044" && b.Name == "Access Bank");
    }
}
