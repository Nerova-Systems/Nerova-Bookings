using Main.Database;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Main.Features.EventTypes.Domain;
using Main.Features.Insights.Shared;
using Main.Features.ManagedEventTypes.EventHandlers;
using Main.Features.ManagedEventTypes.Services;
using Main.Features.Permissions.Pipeline;
using Main.Features.Permissions.Services;
using Main.Features.Scheduling.Notifications;
using Main.Features.Scheduling.Shared;
using Main.Features.Webhooks.Infrastructure;
using Main.Features.Webhooks.Jobs;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.EventHandlers;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Jobs;
using Main.Features.Workflows.Senders;
using Main.Features.Workflows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Configuration;
using SharedKernel.Emails;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;

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
            // PermissionCheckBehavior must be registered BEFORE AddSharedServices so it is the
            // outermost pipeline behavior (runs before Validation — security gate first, business
            // logic second). Mirrors the Account SCS registration order.
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionCheckBehavior<,>));

            return services
                .AddScoped<IPermissionCheckService, PermissionCheckService>()
                .AddScoped<PublicSchedulingResolver>()
                .AddScoped<PublicSlotCalculator>()
                .AddScoped<CollectiveSlotCalculator>()
                .AddScoped<RoundRobinSlotCalculator>()
                .AddScoped<IHostRepository, HostRepository>()
                .AddScoped<InsightsScopeResolver>()
                .AddScoped<ManagedEventTypePropagator>()
                .AddScoped<EventTypeUpdatedManagedSyncHandler>()
                // ─── App platform ──────────────────────────────────────────
                // Singleton state store (15-min TTL in-memory). Connector tracks may swap in a
                // Redis-backed implementation by replacing this registration.
                .AddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>()
                // Registry is singleton: it caches the slug→installer dictionary at construction.
                // It depends on the set of registered IAppInstaller implementations — connector
                // tracks add their installers as singletons; this track ships zero installers.
                .AddSingleton<IAppRegistry, AppRegistry>()
                .AddSingleton<CredentialProtector>()
                // ─── Webhook platform ──────────────────────────────────────
                // Dispatcher is scoped — uses MainDbContext via the repositories. HttpClient
                // factory is required by the worker-side processor; safe to register here so the
                // API can also be wired up (e.g., for synchronous test-fire) without duplicating.
                .AddScoped<IWebhookDispatcher, WebhookDispatcher>()
                .AddHttpClient()
                .AddEmailRendering("WebApp")
                .AddSharedServices<MainDbContext>([Assembly]);
        }

        /// <summary>
        ///     Registers TickerQ with EF Core persistence and all workflow background jobs.
        ///     Called ONLY from Workers/Program.cs — not included in AddMainServices to keep API and test contexts clean.
        /// </summary>
        public IServiceCollection AddMainTickerQ()
        {
            // Workflow services
            services
                .AddScoped<WorkflowBookingReader>()
                .AddScoped<WorkflowReminderScheduler>()
                .AddScoped<BookingCreatedWorkflowHandler>()
                .AddScoped<BookingCancelledWorkflowHandler>()
                .AddScoped<BookingRescheduledWorkflowHandler>()
                .AddSingleton<ISmsSender, StubSmsSender>()
                .AddSingleton<IWhatsappSender, StubWhatsappSender>()
                // Cross-SCS host lookup (account-database). Scoped because it opens an Npgsql
                // connection per call; the connection is awaited and disposed within the call.
                .AddScoped<IUserContactLookup, AccountDbUserContactLookup>()
                .AddScoped<IHostEmailProvider, HostEmailProvider>()
                .AddScoped<IBookingNotificationDispatcher, BookingNotificationDispatcher>();

            // TickerQ with EF Core persistence (tables added to MainDbContext via model customizer)
            services.AddTickerQ(opt =>
            {
                opt.AddOperationalStore(ef =>
                ef.UseApplicationDbContext<MainDbContext>(TickerQ.EntityFrameworkCore.Customizer.ConfigurationType.UseModelCustomizer)
                );
            });

            // Register cron jobs — run every 60 seconds
            services.MapTicker<WorkflowSchedulerJob>()
                .WithCron("*/1 * * * *");

            services.MapTicker<DispatchWorkflowReminderJob>()
                .WithCron("*/1 * * * *");

            // ─── Webhook delivery worker ──────────────────────────────────
            // The processor encapsulates the HTTP + backoff behaviour so it can be unit-tested
            // without the TickerQ host; the job is the cron entry point that batches due rows.
            services.AddScoped<WebhookDeliveryProcessor>();

            services.MapTicker<WebhookDeliveryJob>()
                .WithCron("*/1 * * * *");

            return services;
        }
    }
}
