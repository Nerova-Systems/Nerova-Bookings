using FluentAssertions;
using SharedKernel.SinglePageApp;
using Xunit;

namespace SharedKernel.Tests.SinglePageApp;

public sealed class SinglePageAppConfigurationTests
{
    [Fact]
    public void ContentSecurityPolicies_ShouldAllowPaystackCheckoutHosts()
    {
        // Arrange & Act
        var configuration = new SinglePageAppConfiguration(
            true,
            null,
            Path.Combine("account", "WebApp"),
            "https://app.dev.localhost:9000",
            "https://app.dev.localhost:9000/account"
        );

        // Assert
        configuration.ContentSecurityPolicies.Should().Contain("https://js.paystack.co");
        configuration.ContentSecurityPolicies.Should().Contain("https://api.paystack.co");
        configuration.ContentSecurityPolicies.Should().Contain("https://checkout.paystack.com");
    }
}
