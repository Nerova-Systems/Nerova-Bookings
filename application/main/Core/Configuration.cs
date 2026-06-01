using Main.Database;
using Main.Features.WhatsAppMessaging.Shared;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Integrations.Meta;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddHttpClient<MetaGraphClient>(client =>
                {
                    client.BaseAddress = new Uri("https://graph.facebook.com/");
                    client.Timeout = TimeSpan.FromSeconds(10);
                }
            );
            services.AddKeyedScoped<IMetaGraphClient>("meta", (serviceProvider, _) => serviceProvider.GetRequiredService<MetaGraphClient>());
            services.AddKeyedScoped<IMetaGraphClient, MockMetaGraphClient>("mock-meta");
            services.AddScoped<MetaGraphClientFactory>();
            services.AddScoped<WhatsAppAccessTokenProtector>();
            services.AddScoped<ProcessPendingWhatsAppEvents>();

            return services.AddSharedServices<MainDbContext>([Assembly]);
        }
    }
}
