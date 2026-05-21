using Account.Database;
using Account.Features.ApiKeys.Infrastructure;
using Account.Features.AuditLog.Domain;
using Account.Features.AuditLog.Infrastructure;
using Account.Features.Smtp.Infrastructure;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.DelegationCredentials.Infrastructure;
using Account.Features.EmailAuthentication.Shared;
using Account.Features.ExternalAuthentication;
using Account.Features.ExternalAuthentication.Shared;
using Account.Features.FeatureFlags.Shared;
using Account.Features.Permissions.Pipeline;
using Account.Features.Permissions.Services;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Shared;
using Account.Integrations.Gravatar;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SharedKernel.AuditLog;
using SharedKernel.Authentication.ApiKey;
using Account.Integrations.OAuth;
using Account.Integrations.OAuth.Google;
using Account.Integrations.OAuth.Mock;
using Account.Integrations.Paystack;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SharedKernel.Configuration;
using SharedKernel.Cqrs;
using SharedKernel.DelegationCredentials;
using SharedKernel.Emails;
using SharedKernel.Integrations.Email;
using SharedKernel.OpenIdConnect;

namespace Account;

public static class Configuration
{
    public static Assembly Assembly => Assembly.GetExecutingAssembly();

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddAccountInfrastructure()
        {
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
            // PermissionCheckBehavior must be registered BEFORE AddSharedServices so it is the outermost
            // pipeline behavior (runs before Validation — security gate first, business logic second).
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionCheckBehavior<,>));

            services.AddHttpClient<GravatarClient>(client =>
                {
                    client.BaseAddress = new Uri("https://gravatar.com/");
                    client.Timeout = TimeSpan.FromSeconds(2);
                }
            );

            services.AddHttpClient<OpenIdConnectConfigurationManagerFactory>(client => { client.Timeout = TimeSpan.FromSeconds(10); });
            services.AddSingleton<OpenIdConnectConfigurationManagerFactory>();

            services.AddHttpClient<ExternalAvatarClient>(client => { client.Timeout = TimeSpan.FromSeconds(10); });

            services.AddHttpClient<GoogleOAuthProvider>(client => { client.Timeout = TimeSpan.FromSeconds(10); });
            services.AddKeyedScoped<IOAuthProvider, GoogleOAuthProvider>("google");
            services.AddKeyedScoped<IOAuthProvider, MockOAuthProvider>("mock-google");
            services.AddScoped<OAuthProviderFactory>();

            services.AddEmailRendering("WebApp");

            services.AddMemoryCache();
            services.AddOptions<PaystackOptions>()
                .BindConfiguration("Paystack")
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<PaystackOptions>, PaystackOptionsValidator>();

            services.AddHttpContextAccessor();
            services.AddSingleton<MockPaystackState>();
            services.AddSingleton<PlatformCurrencyProvider>();
            services.AddSingleton<IPlatformCurrencyProvider>(sp => sp.GetRequiredService<PlatformCurrencyProvider>());
            services.AddHostedService<PlatformCurrencyStartupResolver>();
            services.AddKeyedScoped<IPaystackClient, PaystackClient>("paystack");
            services.AddKeyedScoped<IPaystackClient, MockPaystackClient>("mock-paystack");
            services.AddKeyedScoped<IPaystackClient, UnconfiguredPaystackClient>("unconfigured-paystack");
            services.AddScoped<PaystackClientFactory>();

            return services
                .AddSharedServices<AccountDbContext>([Assembly])
                .AddScoped<SmtpCredentialProtector>()
                .AddScoped<DelegationCredentialEncryption>()
                .AddScoped<IDelegationCredentialRepository, DelegationCredentialRepository>()
                .AddScoped<IDelegationCredentialResolver, DelegationCredentialResolver>()
                .AddScoped<IDelegationCredentialTester, NotConfiguredDelegationCredentialTester>()
                .Decorate<IEmailClient, TenantAwareEmailClient>()
                .AddScoped<StartEmailConfirmation>()
                .AddScoped<CompleteEmailConfirmation>()
                .AddScoped<AvatarUpdater>()
                .AddScoped<FeatureFlagEvaluator>()
                .AddScoped<PlanBasedFeatureFlagEvaluator>()
                .AddScoped<UserInfoFactory>()
                .AddScoped<ProcessPendingPaystackEvents>()
                .AddScoped<ProcessSubscriptionBilling>()
                .AddScoped<ExternalAuthenticationService>()
                .AddScoped<ExternalAuthenticationHelper>()
                .AddScoped<IPermissionCheckService, PermissionCheckService>()
                .AddScoped<IAuditLogRepository, AuditLogRepository>()
                .AddScoped<IAuditLogEmitter, AuditLogEmitter>()
                .AddScoped<IApiKeyValidator, ApiKeyValidator>()
                .AddApiKeyAuthentication();
        }

        private IServiceCollection AddApiKeyAuthentication()
        {
            // Register the API key authentication scheme alongside the existing JWT scheme.
            // The JWT scheme's ForwardDefaultSelector will route nerova_* tokens here.
            services
                .AddAuthentication()
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationDefaults.SchemeName, _ => { }
                );

            // Forward requests that carry a Nerova API key token to the ApiKey scheme
            // before the JWT bearer handler has a chance to reject them.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opts =>
            {
                opts.ForwardDefaultSelector = ctx =>
                {
                    if (ctx.Request.Headers.ContainsKey("X-Api-Key"))
                        return ApiKeyAuthenticationDefaults.SchemeName;

                    var auth = ctx.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer " + ApiKeyAuthenticationDefaults.TokenPrefix, StringComparison.OrdinalIgnoreCase))
                        return ApiKeyAuthenticationDefaults.SchemeName;

                    return null;
                };
            });

            return services;
        }
    }
}