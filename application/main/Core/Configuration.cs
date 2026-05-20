using Main.Database;
using Main.Features.BookingSideEffects.Workers;
using Main.Features.Connectors.Domain;
using Main.Features.Scheduling.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SharedKernel.Configuration;

namespace Main;

public static class Configuration
{
    public static Assembly Assembly => Assembly.GetExecutingAssembly();

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddMainInfrastructure()
        {
            // Infrastructure is configured separately from other Infrastructure services to allow mocking in tests
            return builder.AddSharedInfrastructure<MainDbContext>("main-database");
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddMainServices()
        {
            services.TryAddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.TryAddSingleton<IHostEnvironment>(new MainFallbackHostEnvironment());

            return services
                .AddHttpClient()
                .AddScoped<BookingSideEffectProcessor>()
                .AddScoped<FakeCoreConnectorClient>()
                .AddScoped<IConnectorTokenStore, ProtectedConnectorTokenStore>()
                .AddScoped<CoreConnectorOAuthProviderRegistry>()
                .AddScoped<ICoreConnectorOAuthProvider, GoogleCalendarOAuthProvider>()
                .AddScoped<ICoreConnectorOAuthProvider, Office365CalendarOAuthProvider>()
                .AddScoped<ICoreConnectorOAuthProvider, ZoomOAuthProvider>()
                .AddScoped<ICoreConnectorAccessTokenProvider, ProtectedCoreConnectorAccessTokenProvider>()
                .AddScoped<ICoreConnectorProvider, GoogleCalendarCoreConnectorProvider>()
                .AddScoped<ICoreConnectorProvider, Office365CalendarCoreConnectorProvider>()
                .AddScoped<ICoreCalendarConnectorProvider, GoogleCalendarCoreConnectorProvider>()
                .AddScoped<ICoreCalendarConnectorProvider, Office365CalendarCoreConnectorProvider>()
                .AddScoped<ICoreConferencingConnectorProvider, ZoomCoreConnectorProvider>()
                .AddScoped<ICoreConnectorClient, CoreConnectorClient>()
                .AddScoped<PublicSchedulingResolver>()
                .AddScoped<PublicSlotCalculator>()
                .AddSharedServices<MainDbContext>([Assembly]);
        }
    }

    private sealed class MainFallbackHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Main";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
