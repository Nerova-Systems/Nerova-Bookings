using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using Account.Database;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Impersonation;

/// <summary>
///     Integration tests for the impersonation end flow:
///     <c>POST /api/account/impersonation/end</c>.
/// </summary>
public sealed class EndImpersonationTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JwtSecurityToken DecodeToken(HttpResponseMessage response)
    {
        var token = response.Headers.GetValues("x-access-token").Single();
        return new JwtSecurityTokenHandler().ReadJwtToken(token);
    }

    private void SetImpersonationToken(UserId targetUserId, UserId actorUserId, TenantId targetTenantId)
    {
        // Simulate a token issued by StartImpersonation — sub = target, impersonated_by = actor
        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Id = targetUserId,
            TenantId = targetTenantId,
            Role = "Member",
            Email = "target@test.com",
            Locale = "en-US",
            ImpersonatedByIdentifier = actorUserId.ToString(),
            ImpersonatedByUserId = actorUserId
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void SetRegularToken(UserId userId, TenantId tenantId)
    {
        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Id = userId,
            TenantId = tenantId,
            Role = "Owner",
            Email = "owner@test.com",
            Locale = "en-US"
            // No ImpersonatedByIdentifier
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndImpersonation_HappyPath_ShouldReturnActorToken()
    {
        // Arrange
        // The "current user" is Tenant1Member being impersonated; actor is Tenant1Owner (already seeded).
        SetImpersonationToken(
            targetUserId: DatabaseSeeder.Tenant1Member.Id,
            actorUserId: DatabaseSeeder.Tenant1Owner.Id,
            targetTenantId: DatabaseSeeder.Tenant1.Id
        );

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/impersonation/end", null);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Subject.Should().Be(DatabaseSeeder.Tenant1Owner.Id.ToString());
        jwt.Claims.FirstOrDefault(c => c.Type == "impersonated_by")?.Value.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task EndImpersonation_WhenNotImpersonating_ShouldReturnBadRequest()
    {
        // Arrange — regular token, no impersonated_by claim
        SetRegularToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/impersonation/end", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.BadRequest,
            "Current session is not an impersonated session."
        );
    }
}
