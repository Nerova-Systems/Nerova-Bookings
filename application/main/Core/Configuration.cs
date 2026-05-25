using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.Insights.Shared;
using Main.Features.ManagedEventTypes.EventHandlers;
using Main.Features.ManagedEventTypes.Services;
using Main.Features.Permissions.Pipeline;
using Main.Features.Permissions.Services;
using Main.Features.Scheduling.Shared;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.EventHandlers;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Jobs;
using Main.Features.Workflows.Senders;
using Main.Features.Workflows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Configuration;
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
                .AddSingleton<IHostEmailProvider, StubHostEmailProvider>();

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

            return services;
        }
    }
}
