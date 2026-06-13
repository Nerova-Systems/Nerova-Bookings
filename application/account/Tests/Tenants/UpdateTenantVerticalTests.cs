using System.Net;
using System.Net.Http.Json;
using Account.Database;
using FluentAssertions;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Tenants;

public sealed class UpdateTenantVerticalTests : EndpointBaseTest<AccountDbContext>, IClassFixture<AccountWebApplicationFactory>
{
    public UpdateTenantVerticalTests(AccountWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UpdateTenantVertical_WhenOwner_ShouldPersistAndReturnOnCurrentTenant()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/tenants/current/vertical", new { vertical = "Salon" });

        // Assert
        response.EnsureSuccessStatusCode();
        var tenant = await (await AuthenticatedOwnerHttpClient.GetAsync("/api/account/tenants/current")).DeserializeResponse<TenantVerticalResponse>();
        tenant!.Vertical.Should().Be("Salon");
    }

    [Fact]
    public async Task UpdateTenantVertical_WhenMember_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.PutAsJsonAsync("/api/account/tenants/current/vertical", new { vertical = "Barber" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record TenantVerticalResponse(string Name, string? Vertical);
}
