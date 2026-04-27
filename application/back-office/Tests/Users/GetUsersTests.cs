using BackOffice.Database;
using BackOffice.Features.Catalog.Domain;
using BackOffice.Features.Users.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace BackOffice.Tests.Users;

public sealed class GetUsersTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task GetUsers_WhenSysOpSearchesByEmail_ShouldReturnMatchingUsers()
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

        var response = await AuthenticatedSysOpHttpClient.GetAsync("/api/back-office/users?search=owner%40acme.test");

        response.ShouldBeSuccessfulGetRequest();
        var usersResponse = await response.DeserializeResponse<UsersResponse>();
        usersResponse!.Users.Should().ContainSingle(u => u.Id == user.Id && u.TenantName == "Acme");
    }
}
