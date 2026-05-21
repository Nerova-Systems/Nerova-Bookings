using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.BackOffice;

/// <summary>
///     Integration tests for the back-office impersonation start flow:
///     <c>POST /api/back-office/users/{id}/impersonate</c>.
/// </summary>
public sealed class StartBackOfficeImpersonationTests(BackOfficeWebApplicationFactory factory)
    : BackOfficeEndpointBaseTest(factory), IClassFixture<BackOfficeWebApplicationFactory>
{
    private static JwtSecurityToken DecodeToken(HttpResponseMessage response)
    {
        var token = response.Headers.GetValues("x-access-token").Single();
        return new JwtSecurityTokenHandler().ReadJwtToken(token);
    }

    [Fact]
    public async Task StartBackOfficeImpersonation_WhenAdminImpersonatesExistingUser_ShouldReturnImpersonationToken()
    {
        // Arrange
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/users/{DatabaseSeeder.Tenant1Owner.Id}/impersonate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jwt = DecodeToken(response);
        jwt.Subject.Should().Be(DatabaseSeeder.Tenant1Owner.Id.ToString());
        jwt.Claims.First(c => c.Type == "impersonated_by").Value.Should().Be("backoffice");
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "BackOfficeImpersonationStarted");
    }

    [Fact]
    public async Task StartBackOfficeImpersonation_WhenNonAdminBackOfficeIdentity_ShouldReturnForbidden()
    {
        // Arrange
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/users/{DatabaseSeeder.Tenant1Owner.Id}/impersonate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StartBackOfficeImpersonation_WhenUserNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        var unknownUserId = UserId.NewId();

        // Act
        var response = await client.PostAsync($"/api/back-office/users/{unknownUserId}/impersonate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
