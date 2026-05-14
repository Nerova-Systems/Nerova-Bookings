using System.Net;
using System.Web;
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
                    ["OAuth:Facebook:ClientSecret"] = "facebook-client-secret",
                    ["OAuth:Facebook:LoginConfigurationId"] = "business-login-configuration-id"
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

    [Fact]
    public void BuildAuthorizationUrl_WhenLoginConfigurationIdConfigured_ShouldUseBusinessLoginConfigurationWithoutScope()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OAuth:Facebook:ClientId"] = "facebook-client-id",
                    ["OAuth:Facebook:ClientSecret"] = "facebook-client-secret",
                    ["OAuth:Facebook:LoginConfigurationId"] = "business-login-configuration-id"
                }
            )
            .Build();
        using var httpClient = new HttpClient(new StubHttpMessageHandler());
        var provider = new FacebookOAuthProvider(httpClient, configuration, NullLogger<FacebookOAuthProvider>.Instance);

        // Act
        var authorizationUrl = provider.BuildAuthorizationUrl("state-token", "code-challenge", "nonce", "https://localhost/callback");

        // Assert
        var query = HttpUtility.ParseQueryString(new Uri(authorizationUrl).Query);
        query["config_id"].Should().Be("business-login-configuration-id");
        query["override_default_response_type"].Should().Be("true");
        query["scope"].Should().BeNull();
    }

    [Fact]
    public void BuildAuthorizationUrl_WhenLoginConfigurationIdMissing_ShouldThrowConfigurationError()
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

        // Act
        var act = () =>
        {
            using var httpClient = new HttpClient(new StubHttpMessageHandler());
            _ = new FacebookOAuthProvider(httpClient, configuration, NullLogger<FacebookOAuthProvider>.Instance);
        };

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("OAuth:Facebook:LoginConfigurationId is required for Facebook Login for Business.");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
