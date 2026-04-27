using Account.Database;
using Account.Features.Catalog;
using Account.Features.EmailAuthentication.Shared;
using Account.Features.ExternalAuthentication;
using Account.Features.ExternalAuthentication.Shared;
using Account.Features.Users.Shared;
using Account.Integrations.Gravatar;
using Account.Integrations.OAuth;
using Account.Integrations.OAuth.Google;
using Account.Integrations.OAuth.Mock;
using Account.Integrations.PayFast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Configuration;
using SharedKernel.OpenIdConnect;
using SharedKernel.Outbox;

namespace Account;

public static class Configuration
{
    public static Assembly Assembly => Assembly.GetExecutingAssembly();

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddAccountInfrastructure()
        {
            builder.Services.Configure<PayFastSettings>(builder.Configuration.GetSection("PayFast"));

            // Infrastructure is configured separately from other Infrastructure services to allow mocking in tests
            return builder
                .AddSharedInfrastructure<AccountDbContext>("account-database")
                .AddNamedBlobStorages([("account-storage", "BLOB_STORAGE_URL")]);
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddAccountServices()
        {
            services.AddHttpClient<GravatarClient>(client =>
                {
                    client.BaseAddress = new Uri("https://gravatar.com/");
                    client.Timeout = TimeSpan.FromSeconds(5);
                }
            );

            services.AddHttpClient<OpenIdConnectConfigurationManagerFactory>(client => { client.Timeout = TimeSpan.FromSeconds(10); });
            services.AddSingleton<OpenIdConnectConfigurationManagerFactory>();

            services.AddHttpClient<ExternalAvatarClient>(client => { client.Timeout = TimeSpan.FromSeconds(10); });

            services.AddHttpClient<GoogleOAuthProvider>(client => { client.Timeout = TimeSpan.FromSeconds(10); });
            services.AddKeyedScoped<IOAuthProvider, GoogleOAuthProvider>("google");
            services.AddKeyedScoped<IOAuthProvider, MockOAuthProvider>("mock-google");
            services.AddScoped<OAuthProviderFactory>();

            services.AddHttpClient<IPayFastClient, PayFastClient>(client => { client.Timeout = TimeSpan.FromSeconds(30); });
            services.AddHttpClient("BackOfficeInternal", client =>
                {
                    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("BACK_OFFICE_API_URL") ?? "https://localhost:9200");
                    client.Timeout = TimeSpan.FromSeconds(30);
                }
            );
            services.AddScoped<IOutboxMessageHandler>(provider => CatalogOutboxForwarder.CreateHandlers(provider.GetRequiredService<IHttpClientFactory>()).ElementAt(0));
            services.AddScoped<IOutboxMessageHandler>(provider => CatalogOutboxForwarder.CreateHandlers(provider.GetRequiredService<IHttpClientFactory>()).ElementAt(1));
            services.AddScoped<IOutboxMessageHandler>(provider => CatalogOutboxForwarder.CreateHandlers(provider.GetRequiredService<IHttpClientFactory>()).ElementAt(2));
            services.AddScoped<IOutboxMessageHandler>(provider => CatalogOutboxForwarder.CreateHandlers(provider.GetRequiredService<IHttpClientFactory>()).ElementAt(3));

            return services
                .AddSharedServices<AccountDbContext>([Assembly])
                .AddScoped<StartEmailConfirmation>()
                .AddScoped<CompleteEmailConfirmation>()
                .AddScoped<AvatarUpdater>()
                .AddScoped<UserInfoFactory>()
                .AddScoped<ExternalAuthenticationService>()
                .AddScoped<ExternalAuthenticationHelper>();
        }
    }
}
