using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Account.Database;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.FeatureFlags;

public sealed class FeatureFlagsTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task TenantOverride_WhenEnabled_ShouldExposeFlagForTenant()
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/account/feature-flags/teams/tenant-override",
            new { enabled = true }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var flags = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>("/api/account/feature-flags");
        flags.Should().NotBeNull();
        flags!.Select(flag => flag!.GetValue<string>()).Should().Contain("teams");
    }

    [Fact]
    public async Task UserOverride_WhenDisabled_ShouldOverrideEnabledTenantFlag()
    {
        await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/account/feature-flags/teams/tenant-override", new { enabled = true });

        var disableForMember = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/account/feature-flags/teams/user-override",
            new { userId = DatabaseSeeder.Tenant1Member.Id.ToString(), enabled = false }
        );

        disableForMember.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerFlags = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>("/api/account/feature-flags");
        var memberFlags = await AuthenticatedMemberHttpClient.GetFromJsonAsync<JsonArray>("/api/account/feature-flags");
        ownerFlags!.Select(flag => flag!.GetValue<string>()).Should().Contain("teams");
        memberFlags!.Select(flag => flag!.GetValue<string>()).Should().NotContain("teams");
    }

    [Fact]
    public async Task TeamsEndpoint_WhenFeatureDisabled_ShouldReturnNotFound()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/teams");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Feature 'teams' is not enabled.");
    }
}
