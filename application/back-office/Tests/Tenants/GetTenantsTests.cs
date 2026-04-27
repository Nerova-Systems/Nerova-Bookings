using System.Net;
using BackOffice.Database;
using BackOffice.Features.Catalog.Domain;
using BackOffice.Features.Tenants.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace BackOffice.Tests.Tenants;

public sealed class GetTenantsTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task GetTenants_WhenUserIsNotInternal_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/back-office/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenants_WhenSysOp_ShouldReturnCatalogTenants()
    {
        var tenant = CatalogTenant.Create(TenantId.NewId());
        tenant.Upsert("Acme", "Active", "Trial", null, TimeProvider.GetUtcNow(), null, TimeProvider.GetUtcNow());
        var user = CatalogUser.Create(UserId.NewId());
        user.Upsert(tenant.Id, "owner@acme.test", "Owner", "Ada", "Lovelace", "", true, TimeProvider.GetUtcNow(), null, null, TimeProvider.GetUtcNow());

        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
            dbContext.Add(tenant);
            dbContext.Add(user);
            await dbContext.SaveChangesAsync();
        }

        var response = await AuthenticatedSysOpHttpClient.GetAsync("/api/back-office/tenants");

        response.ShouldBeSuccessfulGetRequest();
        var tenantsResponse = await response.DeserializeResponse<TenantsResponse>();
        tenantsResponse!.Tenants.Should().ContainSingle(t => t.Id == tenant.Id && t.UserCount == 1);
    }
}
