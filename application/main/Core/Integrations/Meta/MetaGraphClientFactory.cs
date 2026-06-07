using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Main.Integrations.Meta;

/// <summary>
///     Resolves the Meta Graph client to use for a request. Mirrors the Stripe client factory: a mock
///     provider can be force-selected via a cookie when <c>Meta:AllowMockProvider</c> is enabled, the real
///     client is used when the app is configured with Meta credentials, and the mock is used as the default
///     fallback so the app boots and onboarding works locally without any Meta secrets.
/// </summary>
public sealed class MetaGraphClientFactory(IServiceProvider serviceProvider, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IHostEnvironment environment)
{
    public const string UseMockProviderCookieName = "__Test_Use_Mock_Provider";

    private readonly bool _allowMockProvider = configuration.GetValue<bool>("Meta:AllowMockProvider");
    private readonly string? _appId = configuration["Meta:AppId"];
    private readonly string? _appSecret = configuration["Meta:AppSecret"];

    public bool IsConfigured => IsValueConfigured(_appId) && IsValueConfigured(_appSecret);

    public IMetaGraphClient GetClient()
    {
        if (ShouldUseMockProvider())
        {
            return serviceProvider.GetRequiredKeyedService<IMetaGraphClient>("mock-meta");
        }

        if (!IsConfigured)
        {
            if (environment.IsDevelopment())
            {
                return serviceProvider.GetRequiredKeyedService<IMetaGraphClient>("mock-meta");
            }

            return serviceProvider.GetRequiredKeyedService<IMetaGraphClient>("unconfigured-meta");
        }

        return serviceProvider.GetRequiredKeyedService<IMetaGraphClient>("meta");
    }

    private static bool IsValueConfigured(string? value)
    {
        return !string.IsNullOrEmpty(value) && value != "not-configured";
    }

    private bool ShouldUseMockProvider()
    {
        if (!_allowMockProvider)
        {
            return false;
        }

        return httpContextAccessor.HttpContext?.Request.Cookies.ContainsKey(UseMockProviderCookieName) == true;
    }
}
