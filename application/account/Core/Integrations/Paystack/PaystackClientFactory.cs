using Account.Integrations.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Account.Integrations.Paystack;

public sealed class PaystackClientFactory(IServiceProvider serviceProvider, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
{
    private readonly bool _allowMockProvider = configuration.GetValue<bool>("Paystack:AllowMockProvider");

    public bool IsPaystackSubscriptionEnabled { get; } = configuration["Paystack:SubscriptionEnabled"] == "true";

    public IPaystackClient GetClient()
    {
        if (ShouldUseMockProvider())
        {
            return serviceProvider.GetRequiredKeyedService<IPaystackClient>("mock-paystack");
        }

        if (IsPaystackSubscriptionEnabled)
        {
            return serviceProvider.GetRequiredKeyedService<IPaystackClient>("paystack");
        }

        return serviceProvider.GetRequiredKeyedService<IPaystackClient>("unconfigured-paystack");
    }

    private bool ShouldUseMockProvider()
    {
        if (!_allowMockProvider)
        {
            return false;
        }

        return httpContextAccessor.HttpContext?.Request.Cookies.ContainsKey(OAuthProviderFactory.UseMockProviderCookieName) == true;
    }
}
