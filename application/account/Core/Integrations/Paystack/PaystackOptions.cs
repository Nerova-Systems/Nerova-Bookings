using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Account.Integrations.Paystack;

public sealed class PaystackOptions
{
    public bool SubscriptionEnabled { get; init; }

    public string? PublicKey { get; init; }

    public string? SecretKey { get; init; }

    public string? StandardPlanCode { get; init; }

    public string? PremiumPlanCode { get; init; }

    public long CardAuthorizationAmountSubunit { get; init; } = 100;

    public bool AllowMockProvider { get; init; }

    public bool AllowMockProviderOutsideDevelopment { get; init; }
}

public sealed class PaystackOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<PaystackOptions>
{
    public ValidateOptionsResult Validate(string? name, PaystackOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.PublicKey) && !options.PublicKey.StartsWith("pk_", StringComparison.Ordinal))
        {
            failures.Add("Paystack:PublicKey must start with 'pk_'.");
        }

        if (!string.IsNullOrWhiteSpace(options.SecretKey) && !options.SecretKey.StartsWith("sk_", StringComparison.Ordinal))
        {
            failures.Add("Paystack:SecretKey must start with 'sk_'.");
        }

        if (options.CardAuthorizationAmountSubunit <= 0)
        {
            failures.Add("Paystack:CardAuthorizationAmountSubunit must be greater than zero.");
        }

        if (options.AllowMockProvider && !IsDevelopmentOrTest() && !options.AllowMockProviderOutsideDevelopment)
        {
            failures.Add("Paystack:AllowMockProvider cannot be enabled outside development/test unless Paystack:AllowMockProviderOutsideDevelopment is explicitly enabled.");
        }

        if (options.SubscriptionEnabled && !options.AllowMockProvider)
        {
            if (string.IsNullOrWhiteSpace(options.PublicKey)) failures.Add("Paystack:PublicKey is required when Paystack subscriptions are enabled.");
            if (string.IsNullOrWhiteSpace(options.SecretKey)) failures.Add("Paystack:SecretKey is required when Paystack subscriptions are enabled.");
            if (string.IsNullOrWhiteSpace(options.StandardPlanCode)) failures.Add("Paystack:StandardPlanCode is required when Paystack subscriptions are enabled.");
            if (string.IsNullOrWhiteSpace(options.PremiumPlanCode)) failures.Add("Paystack:PremiumPlanCode is required when Paystack subscriptions are enabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private bool IsDevelopmentOrTest()
    {
        return hostEnvironment.IsDevelopment()
               || string.Equals(hostEnvironment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase)
               || string.Equals(hostEnvironment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
