using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Configuration;
using Xunit;

namespace AppGateway.Tests;

public sealed class LocalhostRedirectTests(AppGatewayApplicationFactory factory) : IClassFixture<AppGatewayApplicationFactory>
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/some/deep/path", "/some/deep/path")]
    [InlineData("/path?state=foo&code=bar", "/path?state=foo&code=bar")]
    public async Task Request_WhenHostIsLocalhost_ShouldReturn301WithLocationPreservingPathAndQuery(string pathAndQuery, string expectedPathAndQuery)
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var port = scope.ServiceProvider.GetRequiredService<PortAllocation>().AppGateway;
        var expectedLocation = $"https://app.dev.localhost:{port}{expectedPathAndQuery}";
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{port}{pathAndQuery}");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be(expectedLocation);
    }
}
