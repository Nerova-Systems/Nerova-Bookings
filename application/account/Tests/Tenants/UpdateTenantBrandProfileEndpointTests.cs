using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Tenants.Commands;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Tenants;

public sealed class UpdateTenantBrandProfileEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string Url = "/api/account/tenants/current/brand-profile";

    [Fact]
    public async Task Put_WhenOwnerWithValidPayload_ShouldReturn200()
    {
        var command = new UpdateTenantBrandProfileCommand
        {
            BusinessDisplayName = "Acme Corp",
            BrandVertical = MetaBusinessVertical.Retail
        };

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(Url, command);

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task Put_WhenAnonymous_ShouldReturn401()
    {
        var command = new UpdateTenantBrandProfileCommand { BusinessDisplayName = "Acme" };

        var response = await AnonymousHttpClient.PutAsJsonAsync(Url, command);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_WhenNonOwner_ShouldReturn403()
    {
        var command = new UpdateTenantBrandProfileCommand { BusinessDisplayName = "Acme" };

        var response = await AuthenticatedMemberHttpClient.PutAsJsonAsync(Url, command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners are allowed to update the brand profile.");
    }
}
