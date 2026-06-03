using System.Text;
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

    [Fact]
    public void ContentSecurityPolicies_WhenDevelopment_ShouldAllowLocalhostDevServerConnections()
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
        configuration.ContentSecurityPolicies.Should().Contain("wss://localhost:*");
        configuration.ContentSecurityPolicies.Should().Contain("https://localhost:*");
    }

    [Fact]
    public async Task ReadAllTextWithRetry_ShouldRecoverFromTemporaryFileLocks()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"single-page-app-{Guid.NewGuid():N}.html");

        try
        {
            await File.WriteAllTextAsync(tempFilePath, "<html>ready</html>");

            await using var exclusiveLock = new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                await exclusiveLock.DisposeAsync();
            });

            var content = SinglePageAppConfiguration.ReadAllTextWithRetry(tempFilePath, new UTF8Encoding(), maxRetries: 5, retryDelayMs: 50);

            content.Should().Contain("ready");
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
