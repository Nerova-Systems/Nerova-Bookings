using Main.Database;
using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Connectors.GoogleMeet;
using Main.Features.Apps.Connectors.MsTeams;
using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Connectors.WhatsApp;
using Main.Features.Apps.Connectors.Zoom;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Main.Features.Autonomy.Jobs;
using Main.Features.Autonomy.Shared;
using Main.Features.BookingSideEffects.Workers;
using Main.Features.Clients.Domain;
using Main.Features.Connectors.Domain;
using Main.Features.DataImport.Agent;
using Main.Features.EventTypes.Domain;
using Main.Features.Insights.Shared;
using Main.Features.ManagedEventTypes.EventHandlers;
using Main.Features.ManagedEventTypes.Services;
using Main.Features.Payments.Infrastructure;
using Main.Features.Payments.Jobs;
using Main.Features.Payments.Paystack;
using Main.Features.Permissions.Pipeline;
using Main.Features.Permissions.Services;
using Main.Features.Receptionist.Agent;
using Main.Features.Receptionist.Shared;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Notifications;
using Main.Features.Scheduling.Shared;
using Main.Features.TeamMembers.Domain;
using Main.Features.TeamMembers.Infrastructure;
using Main.Features.Webhooks.Infrastructure;
using Main.Features.Webhooks.Jobs;
using Main.Features.WhatsAppBooking;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppMessaging.Shared;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Features.Workflows.EventHandlers;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Jobs;
using Main.Features.Workflows.Senders;
using Main.Features.Workflows.Services;
using Main.Integrations.Ai;
using Main.Integrations.Meta;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SharedKernel.Configuration;
using SharedKernel.Emails;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.Customizer;
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
            services.TryAddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.TryAddSingleton<IHostEnvironment>(new MainFallbackHostEnvironment());

            // PermissionCheckBehavior must be registered BEFORE AddSharedServices so it is the
            // outermost pipeline behavior (runs before Validation — security gate first, business
            // logic second). Mirrors the Account SCS registration order.
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionCheckBehavior<,>));

            // Named HttpClient for the Google Calendar connector — outbound calls to Google's
            // OAuth and Calendar API. Registered separately from the chain below because
            // .AddHttpClient(name) returns IHttpClientBuilder.
            services.AddHttpClient(GoogleCalendarSlug.HttpClientName);

            // Named HttpClient for the Office 365 Calendar connector — outbound calls to the
            // Microsoft identity platform and Microsoft Graph.
            services.AddHttpClient(Office365CalendarSlug.HttpClientName);

            // Named HttpClient for the Zoom connector — outbound calls to Zoom OAuth
            // (zoom.us) and the Zoom REST API (api.zoom.us).
            services.AddHttpClient(ZoomSlug.HttpClientName);

            // Named HttpClients for workflow reminder providers (Twilio SMS, Meta WhatsApp).
            // Registered here so the API + worker contexts both get the IHttpClientFactory bindings.
            services.AddHttpClient(TwilioSmsProvider.HttpClientName);
            services.AddHttpClient(MetaWhatsAppProvider.HttpClientName);

            // Paystack booking-payment link service.
            services.AddHttpClient(PaystackPaymentLinkService.HttpClientName);

            // Meta Graph API client for WhatsApp Embedded Signup onboarding + messaging.
            services.AddHttpClient<MetaGraphClient>(client =>
                {
                    client.BaseAddress = new Uri("https://graph.facebook.com/");
                    client.Timeout = TimeSpan.FromSeconds(10);
                }
            );
            services.AddKeyedScoped<IMetaGraphClient>("meta", (serviceProvider, _) => serviceProvider.GetRequiredService<MetaGraphClient>());
            services.AddKeyedScoped<IMetaGraphClient, MockMetaGraphClient>("mock-meta");
            services.AddKeyedScoped<IMetaGraphClient, UnconfiguredMetaGraphClient>("unconfigured-meta");
            services.AddScoped<MetaGraphClientFactory>();
            services.AddScoped<WhatsAppAccessTokenProtector>();
            services.AddScoped<ProcessPendingWhatsAppEvents>();
            services.AddScoped<IWhatsAppOutboundSender, WhatsAppOutboundSender>();
            services.AddScoped<WhatsAppConversationEngine>();
            services.AddSingleton<WhatsAppFlowCrypto>();
            services.AddScoped<WhatsAppLoginFlowDataEndpoint>();
            services.AddScoped<WhatsAppBookingFlowDataEndpoint>();
            services.AddScoped<IWhatsAppLoginChallengeRepository, WhatsAppLoginChallengeRepository>();
            services.Configure<WhatsAppBookingOptions>(bookingOptions =>
                {
                    bookingOptions.FlowId = Environment.GetEnvironmentVariable("WHATSAPP_BOOKING_FLOW_ID");
                    bookingOptions.LoginFlowId = Environment.GetEnvironmentVariable("WHATSAPP_LOGIN_FLOW_ID");
                }
            );

            // ─── AI Front Desk (docs/agentic-system-spec.md) ───────────
            // Claude via the Anthropic Messages API (api.anthropic.com or a Foundry endpoint). When no
            // API key is configured (tests, fresh local dev) the deterministic ScriptedChatClient keeps
            // the whole agent pipeline exercisable without a model — and the spec's reliability rule
            // holds either way: the deterministic Flows engine remains the fallback for booking.
            services.Configure<AiOptions>(options =>
                {
                    options.ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                    var endpoint = Environment.GetEnvironmentVariable("AI_ENDPOINT");
                    if (!string.IsNullOrWhiteSpace(endpoint)) options.Endpoint = endpoint;
                    var model = Environment.GetEnvironmentVariable("AI_MODEL");
                    if (!string.IsNullOrWhiteSpace(model)) options.Model = model;
                }
            );
            services.AddHttpClient("anthropic", client => { client.Timeout = TimeSpan.FromSeconds(60); });
            services.AddScoped<IChatClient>(serviceProvider =>
                {
                    var aiOptions = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                    if (!aiOptions.Value.IsConfigured) return new ScriptedChatClient();

                    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("anthropic");
                    return new AnthropicChatClient(httpClient, aiOptions, serviceProvider.GetRequiredService<ILogger<AnthropicChatClient>>());
                }
            );
            services.AddScoped<ReceptionistToolCatalog>();
            services.AddScoped<ReceptionistAgentFactory>();
            services.AddScoped<ReceptionistInboundRouter>();
            services.AddSingleton<ReceptionistTurnLockRegistry>();
            services.AddScoped<ColumnMappingInferrer>();

            // ─── Autonomy jobs (docs/maf-autonomy-design.md) ───────────
            // The catalog is code: every job pairs a deterministic detector with command-mediated
            // actions. New jobs register here; the runner discovers them through IAutonomyJob.
            services.AddScoped<IAutonomyJob, PaymentRecoveryJob>();
            services.AddScoped<IAutonomyJob, RebookCancelledJob>();
            services.AddScoped<IAutonomyJob, WeeklyDigestJob>();

            return services
                .AddScoped<IPermissionCheckService, PermissionCheckService>()
                .AddScoped<BookingSideEffectProcessor>()
                .AddScoped<PublicSchedulingResolver>()
                .AddScoped<PublicSlotCalculator>()
                .AddScoped<CollectiveSlotCalculator>()
                .AddScoped<RoundRobinSlotCalculator>()
                .AddScoped<IHostRepository, HostRepository>()
                .AddScoped<ITeamMemberDirectory, AccountDbTeamMemberDirectory>()
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
                // ─── Google Calendar connector ─────────────────────────────
                // Options bound from env vars (set by AppHost). Installer is a singleton because
                // it carries no per-request state; the per-credential GoogleCalendarService is
                // built on demand by the factory (scoped) so token refresh persistence flows
                // through the request-scoped ICredentialRepository.
                .Configure<GoogleCalendarOptions>(opts =>
                    {
                        opts.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_ID") ?? string.Empty;
                        opts.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_SECRET") ?? string.Empty;
                    }
                )
                .AddSingleton<IAppInstaller, GoogleCalendarInstaller>()
                .AddScoped<GoogleCalendarServiceFactory>()
                .AddScoped<IExternalBusyTimeProvider, GoogleCalendarBusyTimeProvider>()
                // ─── Office 365 Calendar connector ─────────────────────────
                // Mirrors the Google wiring: env-bound options, singleton installer (no
                // per-request state), scoped service factory + busy-time provider so the
                // refresh-token persistence flows through the request-scoped repository.
                .Configure<Office365CalendarOptions>(opts =>
                    {
                        opts.ClientId = Environment.GetEnvironmentVariable("OFFICE365_CALENDAR_CLIENT_ID") ?? string.Empty;
                        opts.ClientSecret = Environment.GetEnvironmentVariable("OFFICE365_CALENDAR_CLIENT_SECRET") ?? string.Empty;
                        var tenantId = Environment.GetEnvironmentVariable("OFFICE365_CALENDAR_TENANT_ID");
                        if (!string.IsNullOrWhiteSpace(tenantId)) opts.TenantId = tenantId;
                    }
                )
                .AddSingleton<IAppInstaller, Office365CalendarInstaller>()
                .AddScoped<Office365CalendarServiceFactory>()
                .AddScoped<IExternalBusyTimeProvider, Office365CalendarBusyTimeProvider>()
                .AddScoped<IBookingReferenceRepository, BookingReferenceRepository>()
                // ─── Zoom connector ────────────────────────────────────────
                // Conferencing connector: provides Zoom meeting links via IConferenceLinkProvider.
                // Same shape as the calendar connectors — env-bound options, singleton installer,
                // scoped factory + provider so the per-credential service flows through the
                // request-scoped ICredentialRepository when persisting refreshed tokens.
                .Configure<ZoomOptions>(opts =>
                    {
                        opts.ClientId = Environment.GetEnvironmentVariable("ZOOM_CLIENT_ID") ?? string.Empty;
                        opts.ClientSecret = Environment.GetEnvironmentVariable("ZOOM_CLIENT_SECRET") ?? string.Empty;
                    }
                )
                .AddSingleton<IAppInstaller, ZoomInstaller>()
                .AddScoped<ZoomServiceFactory>()
                .AddScoped<IConferenceLinkProvider, ZoomConferenceLinkProvider>()
                // ─── Google Meet connector ─────────────────────────────────
                // Conferencing connector that piggy-backs on the google-calendar credential —
                // no OAuth flow of its own, no new HttpClient (reuses google-calendar's named
                // client through GoogleCalendarServiceFactory). Installer is singleton (matches
                // the other installers and the registry's lifetime); it scope-resolves
                // ICredentialRepository on each call to check the prerequisite.
                .AddSingleton<IAppInstaller, GoogleMeetInstaller>()
                .AddSingleton<IAppInstaller, WhatsAppInstaller>()
                .AddScoped<IConferenceLinkProvider, GoogleMeetConferenceLinkProvider>()
                // ─── Microsoft Teams connector ─────────────────────────────
                // Conferencing connector that piggy-backs on the office365-calendar credential —
                // no OAuth flow of its own, no new HttpClient (reuses office365-calendar's
                // named client through Office365CalendarServiceFactory). Installer is singleton
                // (matches the other installers and the registry's lifetime); it scope-resolves
                // ICredentialRepository + CredentialProtector on each call to check the
                // prerequisite + verify the existing credential carries the
                // OnlineMeetings.ReadWrite scope.
                .AddSingleton<IAppInstaller, MsTeamsInstaller>()
                .AddScoped<IConferenceLinkProvider, MsTeamsConferenceLinkProvider>()
                // ─── Core connector domain ──────────────────────────────────────
                // OAuth provider registry (singleton: slug → provider mapping, no per-request state).
                .AddSingleton<ICoreConnectorOAuthProvider, GoogleCalendarOAuthProvider>()
                .AddSingleton<ICoreConnectorOAuthProvider, Office365CalendarOAuthProvider>()
                .AddSingleton<ICoreConnectorOAuthProvider, ZoomOAuthProvider>()
                .AddSingleton<CoreConnectorOAuthProviderRegistry>()
                // Token store: persists and retrieves encrypted OAuth tokens (scoped: uses MainDbContext).
                .AddScoped<IConnectorTokenStore, ProtectedConnectorTokenStore>()
                // Access token provider: refreshes tokens via the OAuth provider registry.
                .AddScoped<ICoreConnectorAccessTokenProvider, ProtectedCoreConnectorAccessTokenProvider>()
                // Calendar provider implementations (implement both ICoreConnectorProvider and ICoreCalendarConnectorProvider).
                .AddScoped<ICoreConnectorProvider, GoogleCalendarCoreConnectorProvider>()
                .AddScoped<ICoreCalendarConnectorProvider, GoogleCalendarCoreConnectorProvider>()
                .AddScoped<ICoreConnectorProvider, Office365CalendarCoreConnectorProvider>()
                .AddScoped<ICoreCalendarConnectorProvider, Office365CalendarCoreConnectorProvider>()
                // Conferencing provider implementation.
                .AddScoped<ICoreConferencingConnectorProvider, ZoomCoreConnectorProvider>()
                // Fake client (no external calls; selected when credential IDs carry the fake prefix).
                .AddScoped<FakeCoreConnectorClient>()
                // Core connector client: orchestrates calendar + conferencing provider calls.
                .AddScoped<ICoreConnectorClient, CoreConnectorClient>()
                // Scheduling → conferencing bridge.Resolves the right IConferenceLinkProvider
                // for an event type's location and stamps the join URL + BookingReference
                // onto the persisted booking.
                .AddScoped<ConferenceLinkOrchestrator>()
                // ─── Webhook platform ──────────────────────────────────────
                // Dispatcher is scoped — uses MainDbContext via the repositories. HttpClient
                // factory is required by the worker-side processor; safe to register here so the
                // API can also be wired up (e.g., for synchronous test-fire) without duplicating.
                .AddScoped<IWebhookDispatcher, WebhookDispatcher>()
                // Booking → webhook bridge. Command handlers call this to fan booking lifecycle
                // events (created/cancelled/rescheduled/reported) out to subscribed endpoints.
                .AddScoped<IBookingWebhookNotifier, BookingWebhookNotifier>()
                // ─── Booking notifications (email) ─────────────────────────
                // Booking command handlers (Create/Confirm/Cancel) depend on the dispatcher to
                // send confirmation/cancellation emails to attendee + host. Registered here (not
                // in AddMainTickerQ) so the API context can resolve it. The Workers context
                // calls AddMainServices first, so this registration is shared.
                .AddScoped<IUserContactLookup, AccountDbUserContactLookup>()
                .AddScoped<IBookingNotificationDispatcher, BookingNotificationDispatcher>()
                // ─── Booking payments ──────────────────────────────────────
                // Paystack booking-payment link service; booking-payment webhook verifier +
                // idempotency repository.
                .AddScoped<IPaystackPaymentLinkService, PaystackPaymentLinkService>()
                .AddScoped<IPaystackWebhookVerifier, PaystackWebhookVerifier>()
                .AddScoped<IProcessedPaymentEventRepository, ProcessedPaymentEventRepository>()
                .AddScoped<IClientRepository, ClientRepository>()
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
                // ─── Workflow reminder providers ──────────────────────────
                // Env-bound options + singleton providers (no per-request state — they pull HttpClient
                // from the factory on each call). When env vars are missing the providers short-circuit
                // with NotConfigured so the worker keeps ticking in dev / unconfigured environments.
                .Configure<TwilioOptions>(opts =>
                    {
                        opts.AccountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? string.Empty;
                        opts.AuthToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? string.Empty;
                        opts.FromNumber = Environment.GetEnvironmentVariable("TWILIO_FROM_NUMBER") ?? string.Empty;
                    }
                )
                .AddSingleton<ISmsProvider, TwilioSmsProvider>()
                .AddScoped<IWhatsAppProvider, MetaWhatsAppProvider>()
                .AddScoped<IHostEmailProvider, HostEmailProvider>();

            // TickerQ with EF Core persistence (tables added to MainDbContext via model customizer)
            services.AddTickerQ(opt =>
                {
                    opt.AddOperationalStore(ef =>
                        ef.UseApplicationDbContext<MainDbContext>(ConfigurationType.UseModelCustomizer)
                    );
                }
            );

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

            // ─── Phase 4b: booking-payment lifecycle jobs ─────────────────
            // Both jobs are cron-poll (the only TickerQ pattern in this codebase). Release fires
            // when the payment-pending hold expires; reminder nudges After-Session pending payments
            // that have been outstanding for ReminderWindow.
            services.MapTicker<ReleaseUnpaidBookingJob>()
                .WithCron("*/1 * * * *");

            // ─── Autonomy runner ──────────────────────────────────────────
            // Detects and executes (or suggests) front-desk jobs every 15 minutes. Quiet hours and
            // daily caps are enforced inside the runner command, not by the schedule.
            services.MapTicker<AutonomyTickerJob>()
                .WithCron("*/15 * * * *");

            return services;
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
