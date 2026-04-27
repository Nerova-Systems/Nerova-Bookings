using System.Net;
using Account.Database;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Tenants;

public sealed class RestoreTenantTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task RestoreTenant_WhenTenantWasDeleted_ShouldRestoreTenantAndEnqueueCatalogEvent()
    {
        var tenantId = DatabaseSeeder.Tenant1.Id;
        var deleteResponse = await AuthenticatedOwnerHttpClient.DeleteAsync($"/internal-api/account/tenants/{tenantId}");
        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var restoreResponse = await AuthenticatedOwnerHttpClient.PostAsync($"/internal-api/account/tenants/{tenantId}/restore", null);

        restoreResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();
        var deletedAt = Connection.ExecuteScalar<string>("SELECT deleted_at FROM tenants WHERE id = @id", [new { id = tenantId.ToString() }]);
        deletedAt.Should().BeNull();
        var outboxCount = Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM outbox_messages", []);
        outboxCount.Should().BeGreaterThanOrEqualTo(2);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(e => e.GetType().Name == "TenantRestored");
    }

    [Fact]
    public async Task RestoreTenant_WhenTenantIsNotDeleted_ShouldReturnNotFound()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/internal-api/account/tenants/{DatabaseSeeder.Tenant1.Id}/restore", null);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Deleted tenant with id '{DatabaseSeeder.Tenant1.Id}' not found.");
    }
}
