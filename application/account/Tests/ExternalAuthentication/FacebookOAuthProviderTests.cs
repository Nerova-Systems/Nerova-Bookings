using System.Net;
using Account.Integrations.OAuth.Facebook;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Account.Tests.ExternalAuthentication;

public sealed class FacebookOAuthProviderTests
{
    [Fact]
    public void BuildAuthorizationUrl_WhenGraphApiVersionMissing_ShouldUseDefaultVersion()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OAuth:Facebook:ClientId"] = "facebook-client-id",
                    ["OAuth:Facebook:ClientSecret"] = "facebook-client-secret"
                }
            )
            .Build();
        using var httpClient = new HttpClient(new StubHttpMessageHandler());
        var provider = new FacebookOAuthProvider(httpClient, configuration, NullLogger<FacebookOAuthProvider>.Instance);

        // Act
        var authorizationUrl = provider.BuildAuthorizationUrl("state-token", "code-challenge", "nonce", "https://localhost/callback");

        // Assert
        authorizationUrl.Should().StartWith("https://www.facebook.com/v23.0/dialog/oauth?");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
