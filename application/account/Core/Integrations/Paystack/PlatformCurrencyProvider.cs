namespace Account.Integrations.Paystack;

/// <summary>
///     Singleton exposing the platform currency observed on active Paystack prices at startup.
///     Resolved once by <see cref="PlatformCurrencyStartupResolver" /> via
///     <see cref="IPaystackClient.GetPriceCatalogAsync" /> and cached for the process lifetime.
///     Returns <c>null</c> when Paystack is not configured (the dashboard layer renders gracefully when
///     currency is missing). When Paystack is configured but active prices use multiple currencies, the
///     startup resolver fails fast — the application never observes a mixed-currency state at runtime.
/// </summary>
public interface IPlatformCurrencyProvider
{
    string? Currency { get; }
}

public sealed class PlatformCurrencyProvider : IPlatformCurrencyProvider
{
    public string? Currency { get; private set; }

    internal void SetCurrency(string? currency)
    {
        Currency = currency;
    }
}
