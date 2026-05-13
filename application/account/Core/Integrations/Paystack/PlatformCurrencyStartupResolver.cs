using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Account.Integrations.Paystack;

/// <summary>
///     Resolves the platform currency once at application startup by reading the active Paystack price
///     catalog and validating that every active price uses the same currency. The resolved value is
///     cached on <see cref="PlatformCurrencyProvider" /> for the process lifetime — the platform
///     currency never changes at runtime. When Paystack is not configured (
///     <see cref="UnconfiguredPaystackClient" /> is the active implementation) the provider stays
///     <c>null</c> and consumers handle the missing currency gracefully. When Paystack is configured but
///     resolution fails or returns no currency, startup aborts with a clear exception so the
///     application never serves a missing- or mixed-currency state.
/// </summary>
public sealed class PlatformCurrencyStartupResolver(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    PlatformCurrencyProvider platformCurrencyProvider,
    ILogger<PlatformCurrencyStartupResolver> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var paystackClient = ResolveActivePaystackClient(scope.ServiceProvider);

        if (paystackClient is UnconfiguredPaystackClient)
        {
            logger.LogInformation("Paystack is not configured; platform currency will be null for the process lifetime");
            return;
        }

        var priceCatalog = await paystackClient.GetPriceCatalogAsync(cancellationToken);
        var currencies = priceCatalog.Select(p => p.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var currency = currencies.SingleOrDefault();
        if (currency is null)
        {
            throw new InvalidOperationException("Paystack is configured but the platform currency could not be resolved from active prices.");
        }

        platformCurrencyProvider.SetCurrency(currency.ToUpperInvariant());
        logger.LogInformation("Resolved platform currency '{Currency}' from active Paystack prices", currency);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private IPaystackClient ResolveActivePaystackClient(IServiceProvider scopedServiceProvider)
    {
        // The mock provider is gated per-request by an HTTP cookie at runtime; at startup there is no
        // request, so select directly based on configuration. The mock provider is preferred in test
        // and local-dev runs (Paystack:AllowMockProvider=true) so the resolver populates the provider
        // with the mock's configured currency. Otherwise pick the real Paystack client when configured,
        // or fall back to the unconfigured client.
        var allowMockProvider = configuration.GetValue<bool>("Paystack:AllowMockProvider");
        if (allowMockProvider)
        {
            return scopedServiceProvider.GetRequiredKeyedService<IPaystackClient>("mock-paystack");
        }

        var isPaystackSubscriptionEnabled = configuration["Paystack:SubscriptionEnabled"] == "true";
        if (isPaystackSubscriptionEnabled)
        {
            return scopedServiceProvider.GetRequiredKeyedService<IPaystackClient>("paystack");
        }

        return scopedServiceProvider.GetRequiredKeyedService<IPaystackClient>("unconfigured-paystack");
    }
}
