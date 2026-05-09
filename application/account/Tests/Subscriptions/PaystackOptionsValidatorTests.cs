using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class PaystackOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenSubscriptionEnabledWithoutMockAndMissingRequiredConfig_ShouldFail()
    {
        // Arrange
        var validator = new PaystackOptionsValidator(new TestHostEnvironment("Production"));

        // Act
        var result = validator.Validate(null, new PaystackOptions { SubscriptionEnabled = true });

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("Paystack:SecretKey", StringComparison.Ordinal));
        result.Failures.Should().Contain(f => f.Contains("Paystack:StandardPlanCode", StringComparison.Ordinal));
        result.Failures.Should().Contain(f => f.Contains("Paystack:PremiumPlanCode", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenMockProviderEnabledOutsideDevelopment_ShouldFail()
    {
        // Arrange
        var validator = new PaystackOptionsValidator(new TestHostEnvironment("Production"));

        // Act
        var result = validator.Validate(null, new PaystackOptions { AllowMockProvider = true });

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("AllowMockProvider", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenUsingDevelopmentMockProvider_ShouldAllowMissingPaystackSecrets()
    {
        // Arrange
        var validator = new PaystackOptionsValidator(new TestHostEnvironment(Environments.Development));

        // Act
        var result = validator.Validate(null, new PaystackOptions { SubscriptionEnabled = true, AllowMockProvider = true });

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Account.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
