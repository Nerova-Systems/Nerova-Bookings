using System.Net;
using System.Text.Json;
using Account.Features.ExternalAuthentication.Domain;
using Account.Integrations.OAuth.Mock;
using FluentAssertions;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.ExternalAuthentication;

public sealed class CompleteExternalLinkTests : ExternalAuthenticationTestBase
{
    [Fact]
    public async Task StartExternalLink_WhenAuthenticated_ShouldRedirectToAuthorizationUrl()
    {
        // Act
        var response = await AuthenticatedOwnerNoRedirectHttpClient.GetAsync("/api/account/authentication/Facebook/link/start?returnPath=%2Fuser%2Fpreferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("/api/account/authentication/Facebook/link/callback");
        location.Should().Contain("code=mock-authorization-code");
        location.Should().Contain("state=");

        var externalLoginId = GetExternalLoginIdFromResponse(response);
        var loginType = Connection.ExecuteScalar<string>(
            "SELECT type FROM external_logins WHERE id = @id", [new { id = externalLoginId }]
        );
        loginType.Should().Be(nameof(ExternalLoginType.Link));

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("ExternalAccountLinkStarted");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task StartExternalLink_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await NoRedirectHttpClient.GetAsync("/api/account/authentication/Facebook/link/start");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteExternalLink_WhenValid_ShouldAttachProviderToLoggedInUserAndRedirect()
    {
        // Arrange
        var (callbackUrl, cookies) = await StartLinkFlow(ExternalProviderType.Facebook, "/user/preferences");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await CallAuthenticatedCallback(callbackUrl, cookies);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/user/preferences");

        var externalIdentities = Connection.ExecuteScalar<string>(
            "SELECT external_identities FROM users WHERE id = @id", [new { id = DatabaseSeeder.Tenant1Owner.Id.ToString() }]
        );
        externalIdentities.Should().Contain(nameof(ExternalProviderType.Facebook));
        externalIdentities.Should().Contain(MockOAuthProvider.MockProviderUserId);

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("ExternalAccountLinked");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteExternalLink_WhenIdentityBelongsToAnotherUser_ShouldRedirectToErrorPage()
    {
        // Arrange
        InsertUserWithExternalIdentity("linked-facebook-user@example.com", ExternalProviderType.Facebook, MockOAuthProvider.MockProviderUserId);
        var (callbackUrl, cookies) = await StartLinkFlow(ExternalProviderType.Facebook);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await CallAuthenticatedCallback(callbackUrl, cookies);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/error?error=account_already_exists");

        var externalIdentities = Connection.ExecuteScalar<string>(
            "SELECT external_identities FROM users WHERE id = @id", [new { id = DatabaseSeeder.Tenant1Owner.Id.ToString() }]
        );
        var linkedIdentities = JsonSerializer.Deserialize<JsonElement[]>(externalIdentities);
        linkedIdentities.Should().BeEmpty();

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("ExternalAccountLinkFailed");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }
}
