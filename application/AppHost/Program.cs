using System.Net;
using System.Net.Sockets;
using AppHost;
using Azure.Storage.Blobs;
using Projects;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Configuration;

// Read the port allocation before CreateBuilder so we can set Aspire's dashboard env vars
// (ASPNETCORE_URLS, DOTNET_DASHBOARD_OTLP_ENDPOINT_URL, etc.) before Aspire reads them.
var ports = PortAllocation.Load();

OverrideAspireDashboardEnvironmentVariables(ports);

var builder = DistributedApplication.CreateBuilder(args);

CheckPortAvailability(ports);

var appHostname = builder.Configuration["Hostnames:App"] ?? "app.dev.localhost";
var backOfficeHostname = builder.Configuration["Hostnames:BackOffice"] ?? "back-office.dev.localhost";

var appBaseUrl = $"https://{appHostname}:{ports.AppGateway}";
// Localhost mirrors the Azure post-split topology: back-office traffic bypasses AppGateway and
// hits the consolidated account-api process directly on a dedicated Kestrel port.
var backOfficeBaseUrl = $"https://{backOfficeHostname}:{ports.BackOfficeApi}";

var certificatePassword = await builder.CreateSslCertificateIfNotExists();

SecretManagerHelper.GenerateAuthenticationTokenSigningKey("authentication-token-signing-key");

var (googleOAuthConfigured, googleOAuthClientId, googleOAuthClientSecret) = ConfigureGoogleOAuthParameters();
var coreConnectorOAuth = ConfigureCoreConnectorOAuthParameters();

var (paystackConfigured, paystackPublicKey, paystackSecretKey, paystackStandardPlanCode, paystackPremiumPlanCode, paystackCardAuthorizationAmountSubunit) = ConfigurePaystackParameters();
var paystackFullyConfigured = paystackConfigured
                              && builder.Configuration["Parameters:paystack-public-key"] is not null and not "not-configured"
                              && builder.Configuration["Parameters:paystack-secret-key"] is not null and not "not-configured"
                              && builder.Configuration["Parameters:paystack-standard-plan-code"] is not null and not "not-configured"
                              && builder.Configuration["Parameters:paystack-premium-plan-code"] is not null and not "not-configured";

var postgresPassword = builder.CreateStablePassword("postgres-password");
var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: ports.Postgres)
    .WithDataVolume($"platform-platform{ports.VolumeNameInfix}-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithArgs("-c", "wal_level=logical");

var azureStorage = builder
    .AddAzureStorage("azure-storage")
    .RunAsEmulator(resourceBuilder =>
        {
            resourceBuilder.WithDataVolume($"platform-platform{ports.VolumeNameInfix}-azure-storage-data");
            resourceBuilder.WithBlobPort(ports.Blob);
            resourceBuilder.WithLifetime(ContainerLifetime.Persistent);
        }
    )
    .WithAnnotation(new ContainerImageAnnotation
        {
            Registry = "mcr.microsoft.com",
            Image = "azure-storage/azurite",
            Tag = "latest"
        }
    )
    .AddBlobs("blob-storage");

builder
    .AddContainer("mail-server", "axllent/mailpit")
    .WithHttpEndpoint(ports.MailpitHttp, 8025)
    .WithEndpoint(ports.MailpitSmtp, 1025)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithUrlForEndpoint("http", u => u.DisplayText = "Read mail here");

CreateBlobContainer("avatars");
CreateBlobContainer("logos");

var frontendBuild = builder
    .AddJavaScriptApp("frontend-build", "../")
    .WithEnvironment("CERTIFICATE_PASSWORD", certificatePassword)
    .WithEnvironment("MAIN_STATIC_PORT", ports.MainStatic.ToString())
    .WithEnvironment("ACCOUNT_STATIC_PORT", ports.AccountStatic.ToString())
    .WithEnvironment("BACK_OFFICE_STATIC_PORT", ports.BackOfficeStatic.ToString());

var accountDatabase = postgres
    .AddDatabase("account-database", "account");

var accountWorkers = builder
    .AddProject<Account_Workers>("account-workers")
    .WithEnvironment("KESTREL_PORT", ports.AccountWorkers.ToString())
    .WithReference(accountDatabase)
    .WithReference(azureStorage)
    // The BillingDriftWorker resolves PaystackClientFactory which reads these. Without them the worker
    // process sees UnconfiguredPaystackClient even when Paystack is configured at the API level.
    .WithEnvironment("Paystack__SubscriptionEnabled", paystackFullyConfigured ? "true" : "false")
    .WithEnvironment("Paystack__PublicKey", paystackPublicKey)
    .WithEnvironment("Paystack__SecretKey", paystackSecretKey)
    .WithEnvironment("Paystack__StandardPlanCode", paystackStandardPlanCode)
    .WithEnvironment("Paystack__PremiumPlanCode", paystackPremiumPlanCode)
    .WithEnvironment("Paystack__CardAuthorizationAmountSubunit", paystackCardAuthorizationAmountSubunit)
    .WithEnvironment("Paystack__AllowMockProvider", "true")
    .WaitFor(accountDatabase);

var accountApi = builder
    .AddProject<Account_Api>("account-api")
    .WithEnvironment("KESTREL_PORT", ports.AccountApi.ToString())
    // Second Kestrel port for back-office.dev.localhost so localhost mirrors the Azure post-split
    // topology where back-office has its own external ingress and AppGateway is not in the path.
    .WithEnvironment("BACK_OFFICE_KESTREL_PORT", ports.BackOfficeApi.ToString())
    // BackOfficeDevStaticProxy forwards /static/* and HMR traffic on the back-office Kestrel listener
    // to the rsbuild dev server. Dev-only; production builds serve a baked bundle from disk.
    .WithEnvironment("BACK_OFFICE_STATIC_PORT", ports.BackOfficeStatic.ToString())
    // Back-office bundle URLs target the dedicated Kestrel port directly (no AppGateway).
    .WithEnvironment("BACK_OFFICE_PUBLIC_URL", backOfficeBaseUrl)
    .WithEnvironment("BACK_OFFICE_CDN_URL", backOfficeBaseUrl)
    .WithUrlConfiguration(appHostname, ports.AppGateway, "/account")
    // Google OAuth's redirect_uri whitelist requires literal 'localhost', not subdomains like
    // 'app.dev.localhost'. The callback then 301's via LocalhostRedirectMiddleware back to the
    // canonical 'app.dev.localhost' so OAuth-state session cookies flow with the redirected request.
    .WithEnvironment("OAUTH_PUBLIC_URL", "https://localhost:" + ports.AppGateway)
    .WithEnvironment("Hostnames__App", appHostname)
    .WithEnvironment("BackOffice__Host", backOfficeHostname)
    .WithEnvironment("BackOffice__AdminsGroupId", MockEasyAuthIdentities.MockAdminsGroupId)
    .WithReference(accountDatabase)
    .WithReference(azureStorage)
    .WithEnvironment("OAuth__Google__ClientId", googleOAuthClientId)
    .WithEnvironment("OAuth__Google__ClientSecret", googleOAuthClientSecret)
    .WithEnvironment("OAuth__AllowMockProvider", "true")
    .WithEnvironment("Paystack__SubscriptionEnabled", paystackFullyConfigured ? "true" : "false")
    .WithEnvironment("Paystack__PublicKey", paystackPublicKey)
    .WithEnvironment("Paystack__SecretKey", paystackSecretKey)
    .WithEnvironment("Paystack__StandardPlanCode", paystackStandardPlanCode)
    .WithEnvironment("Paystack__PremiumPlanCode", paystackPremiumPlanCode)
    .WithEnvironment("Paystack__CardAuthorizationAmountSubunit", paystackCardAuthorizationAmountSubunit)
    .WithEnvironment("Paystack__AllowMockProvider", "true")
    .WithEnvironment("PUBLIC_GOOGLE_OAUTH_ENABLED", googleOAuthConfigured ? "true" : "false")
    // Force-on so newcomers see the back-office billing UI without Paystack configured. Set to "false" (or
    // change back to `paystackFullyConfigured ? "true" : "false"`) to hide all billing/revenue/Paystack data.
    .WithEnvironment("PUBLIC_SUBSCRIPTION_ENABLED", "true")
    .WaitFor(accountWorkers);

var mainDatabase = postgres
    .AddDatabase("main-database", "main");

var mainWorkers = builder
    .AddProject<Main_Workers>("main-workers")
    .WithEnvironment("KESTREL_PORT", ports.MainWorkers.ToString())
    .WithEnvironment("Connectors__Core__OAuth__GoogleCalendar__ClientId", coreConnectorOAuth.GoogleCalendarClientId)
    .WithEnvironment("Connectors__Core__OAuth__GoogleCalendar__ClientSecret", coreConnectorOAuth.GoogleCalendarClientSecret)
    .WithEnvironment("Connectors__Core__OAuth__Office365Calendar__ClientId", coreConnectorOAuth.Office365CalendarClientId)
    .WithEnvironment("Connectors__Core__OAuth__Office365Calendar__ClientSecret", coreConnectorOAuth.Office365CalendarClientSecret)
    .WithEnvironment("Connectors__Core__OAuth__Zoom__ClientId", coreConnectorOAuth.ZoomClientId)
    .WithEnvironment("Connectors__Core__OAuth__Zoom__ClientSecret", coreConnectorOAuth.ZoomClientSecret)
    .WithReference(mainDatabase)
    // Workers resolve host (booking owner) email/locale by reading the account-database users table.
    // Cross-SCS read-only lookup; no cross-write. Booking notification emails depend on this; without
    // it, host-side notifications and EmailHost workflow reminders would silently no-op.
    .WithReference(accountDatabase)
    .WithReference(azureStorage)
    .WaitFor(mainDatabase);

var mainApi = builder
    .AddProject<Main_Api>("main-api")
    .WithEnvironment("KESTREL_PORT", ports.MainApi.ToString())
    .WithUrlConfiguration(appHostname, ports.AppGateway, "")
    .WithEnvironment("Connectors__Core__OAuth__PublicUrl", appBaseUrl)
    .WithEnvironment("Connectors__Core__OAuth__GoogleCalendar__ClientId", coreConnectorOAuth.GoogleCalendarClientId)
    .WithEnvironment("Connectors__Core__OAuth__GoogleCalendar__ClientSecret", coreConnectorOAuth.GoogleCalendarClientSecret)
    .WithEnvironment("Connectors__Core__OAuth__Office365Calendar__ClientId", coreConnectorOAuth.Office365CalendarClientId)
    .WithEnvironment("Connectors__Core__OAuth__Office365Calendar__ClientSecret", coreConnectorOAuth.Office365CalendarClientSecret)
    .WithEnvironment("Connectors__Core__OAuth__Zoom__ClientId", coreConnectorOAuth.ZoomClientId)
    .WithEnvironment("Connectors__Core__OAuth__Zoom__ClientSecret", coreConnectorOAuth.ZoomClientSecret)
    .WithReference(mainDatabase)
    .WithReference(azureStorage)
    .WithEnvironment("PUBLIC_GOOGLE_OAUTH_ENABLED", googleOAuthConfigured ? "true" : "false")
    .WithEnvironment("PUBLIC_SUBSCRIPTION_ENABLED", paystackFullyConfigured ? "true" : "false")
    .WaitFor(mainWorkers);

builder
    .AddProject<AppGateway>("app-gateway")
    .WithReference(frontendBuild)
    .WithReference(accountApi)
    .WithReference(mainApi)
    .WaitFor(accountApi)
    .WaitFor(frontendBuild)
    .WithEnvironment("ASPNETCORE_URLS", "https://localhost:" + ports.AppGateway)
    .WithEnvironment("Hostnames__App", appHostname)
    .WithUrls(context =>
        {
            // Replace the auto-published "https" endpoint URL with three explicit dashboard URLs.
            // DisplayOrder: higher values sort higher in the list (Web App > Back Office > Open API).
            context.Urls.Clear();
            context.Urls.Add(new ResourceUrlAnnotation { Url = appBaseUrl, DisplayText = "Web App", DisplayOrder = 300 });
            context.Urls.Add(new ResourceUrlAnnotation { Url = backOfficeBaseUrl, DisplayText = "Back Office", DisplayOrder = 200 });
            context.Urls.Add(new ResourceUrlAnnotation { Url = $"{appBaseUrl}/openapi", DisplayText = "Open API", DisplayOrder = 100 });
        }
    );

await builder.Build().RunAsync();

return;

(bool Configured, IResourceBuilder<ParameterResource> ClientId, IResourceBuilder<ParameterResource> ClientSecret) ConfigureGoogleOAuthParameters()
{
    _ = builder.AddParameter("google-oauth-enabled")
        .WithDescription("""
                         **Google OAuth** -- Enables "Sign in with Google" for login and signup using OpenID Connect with PKCE.

                         **Important**: Set up OAuth credentials in the [Google Cloud Console](https://console.cloud.google.com/apis/credentials) and configure them according to the guide in README.md **before** enabling this.

                         - Enter `true` to enable Google OAuth, or `false` to skip. This can be changed later.
                         - After enabling, **restart Aspire** to be prompted for the Client ID and Client Secret.

                         See **README.md** for full setup instructions.
                         """, true
        );

    var configured = builder.Configuration["Parameters:google-oauth-enabled"] == "true";

    if (configured)
    {
        var clientId = builder.AddParameter("google-oauth-client-id", true)
            .WithDescription("""
                             Google OAuth Client ID from the [Google Cloud Console](https://console.cloud.google.com/apis/credentials). The format is `<id>.apps.googleusercontent.com`.

                             **After entering this and the Client Secret, restart Aspire** to apply the configuration.

                             See **README.md** for full setup instructions.
                             """, true
            );
        var clientSecret = builder.AddParameter("google-oauth-client-secret", true)
            .WithDescription("""
                             Google OAuth Client Secret from the [Google Cloud Console](https://console.cloud.google.com/apis/credentials).

                             **After entering this and the Client ID, restart Aspire** to apply the configuration.

                             See **README.md** for full setup instructions.
                             """, true
            );

        return (configured, clientId, clientSecret);
    }

    return (
        configured,
        builder.CreateResourceBuilder(new ParameterResource("google-oauth-client-id", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("google-oauth-client-secret", _ => "not-configured", true))
    );
}

CoreConnectorOAuthParameters ConfigureCoreConnectorOAuthParameters()
{
    var googleCalendar = ConfigureCoreConnectorOAuthParameter("google-calendar", "Google Calendar", "Google Cloud Console");
    var office365Calendar = ConfigureCoreConnectorOAuthParameter("office365-calendar", "Office 365 Calendar", "Microsoft Entra admin center");
    var zoom = ConfigureCoreConnectorOAuthParameter("zoom-video", "Zoom", "Zoom App Marketplace");

    return new CoreConnectorOAuthParameters(
        googleCalendar.ClientId,
        googleCalendar.ClientSecret,
        office365Calendar.ClientId,
        office365Calendar.ClientSecret,
        zoom.ClientId,
        zoom.ClientSecret
    );
}

(IResourceBuilder<ParameterResource> ClientId, IResourceBuilder<ParameterResource> ClientSecret) ConfigureCoreConnectorOAuthParameter(
    string parameterPrefix,
    string displayName,
    string providerPortal
)
{
    _ = builder.AddParameter($"{parameterPrefix}-oauth-enabled")
        .WithDescription($"""
                          **{displayName} connector OAuth** -- Enables Cal.com-style provider connection for booking calendars and conferencing.

                          Configure the OAuth app in the {providerPortal}, then restart Aspire after enabling.
                          """, true
        );

    var configured = builder.Configuration[$"Parameters:{parameterPrefix}-oauth-enabled"] == "true";
    if (!configured)
    {
        return (
            builder.CreateResourceBuilder(new ParameterResource($"{parameterPrefix}-oauth-client-id", _ => "not-configured", true)),
            builder.CreateResourceBuilder(new ParameterResource($"{parameterPrefix}-oauth-client-secret", _ => "not-configured", true))
        );
    }

    var clientId = builder.AddParameter($"{parameterPrefix}-oauth-client-id", true)
        .WithDescription($"{displayName} connector OAuth Client ID from the {providerPortal}.", true);
    var clientSecret = builder.AddParameter($"{parameterPrefix}-oauth-client-secret", true)
        .WithDescription($"{displayName} connector OAuth Client Secret from the {providerPortal}.", true);
    return (clientId, clientSecret);
}

(bool Configured, IResourceBuilder<ParameterResource> PublicKey, IResourceBuilder<ParameterResource> SecretKey, IResourceBuilder<ParameterResource> StandardPlanCode, IResourceBuilder<ParameterResource> PremiumPlanCode, IResourceBuilder<ParameterResource> CardAuthorizationAmountSubunit) ConfigurePaystackParameters()
{
    _ = builder.AddParameter("paystack-enabled")
        .WithDescription("""
                         **Paystack Integration** -- Enables Paystack popup checkout, reusable card authorizations, app-owned subscription billing, plan pricing, retries, and refunds.

                         **Important**: Set up a [Paystack sandbox environment](https://dashboard.paystack.com) and configure it according to the guide in README.md **before** enabling this.

                         - Enter `true` to enable Paystack, or `false` to skip. This can be changed later.
                         - Setup requires the public key, secret key, Standard plan code, Premium plan code, and small card authorization charge amount in subunits.

                         See **README.md** for full setup instructions.
                         """, true
        );

    var configured = builder.Configuration["Parameters:paystack-enabled"] == "true";

    if (configured)
    {
        var publicKey = builder.AddParameter("paystack-public-key", true)
            .WithDescription("""
                             Paystack Public Key from the [Paystack Dashboard](https://dashboard.paystack.com/#/settings/developer). Starts with `pk_test_` or `pk_live_`.

                             **After entering this and the Secret Key, restart Aspire.**

                             See **README.md** for full setup instructions.
                             """, true
            );
        var secretKey = builder.AddParameter("paystack-secret-key", true)
            .WithDescription("""
                             Paystack Secret Key from the [Paystack Dashboard](https://dashboard.paystack.com/#/settings/developer). Starts with `sk_test_` or `sk_live_`.

                             Used for Paystack REST APIs and official HMAC SHA512 webhook verification.

                             See **README.md** for full setup instructions.
                             """, true
            );
        var standardPlanCode = builder.AddParameter("paystack-standard-plan-code", true)
            .WithDescription("Paystack plan code for the Standard subscription plan.", true);
        var premiumPlanCode = builder.AddParameter("paystack-premium-plan-code", true)
            .WithDescription("Paystack plan code for the Premium subscription plan.", true);
        var cardAuthorizationAmountSubunit = builder.Configuration["Parameters:paystack-card-authorization-amount-subunit"] is not null
            ? builder.AddParameter("paystack-card-authorization-amount-subunit", true)
                .WithDescription("Small card authorization charge amount in Paystack subunits, for example `100` for 1.00 in a two-decimal currency.", true)
            : builder.CreateResourceBuilder(new ParameterResource("paystack-card-authorization-amount-subunit", _ => "100", true));

        return (configured, publicKey, secretKey, standardPlanCode, premiumPlanCode, cardAuthorizationAmountSubunit);
    }

    return (
        configured,
        builder.CreateResourceBuilder(new ParameterResource("paystack-public-key", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-secret-key", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-standard-plan-code", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-premium-plan-code", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-card-authorization-amount-subunit", _ => "100", true))
    );
}

void CreateBlobContainer(string containerName)
{
    // Build the Azurite connection string dynamically from the actual blob port so this works on
    // any base port (parallel stacks from git worktrees rely on this). The default development
    // account key is the well-known Azurite credential and is safe to keep in source.
    var connectionString =
        $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:{ports.Blob}/devstoreaccount1";

    new Task(() =>
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            containerClient.CreateIfNotExists();
        }
    ).Start();
}

void OverrideAspireDashboardEnvironmentVariables(PortAllocation portAllocation)
{
    // Must be set before DistributedApplication.CreateBuilder so Aspire picks them up.
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"https://localhost:{portAllocation.Aspire}");
    Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", $"https://localhost:{portAllocation.OtelEndpoint}");
    Environment.SetEnvironmentVariable("DOTNET_RESOURCE_SERVICE_ENDPOINT_URL", $"https://localhost:{portAllocation.ResourceService}");
}

void CheckPortAvailability(PortAllocation portAllocation)
{
    var portsToCheck = new[]
    {
        (portAllocation.ResourceService, "Resource Service"),
        (portAllocation.OtelEndpoint, "Dashboard"),
        (portAllocation.Aspire, "Aspire")
    };
    var blocked = portsToCheck.Where(p => !IsPortAvailable(p.Item1)).ToList();

    if (blocked.Count > 0)
    {
        Console.WriteLine($"⚠️  Port conflicts: {string.Join(", ", blocked.Select(b => $"{b.Item1} ({b.Item2})"))}");
        Console.WriteLine("   Services already running. Stop them first using 'run --stop'.");
        Environment.Exit(1);
    }

    bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed record CoreConnectorOAuthParameters(
    IResourceBuilder<ParameterResource> GoogleCalendarClientId,
    IResourceBuilder<ParameterResource> GoogleCalendarClientSecret,
    IResourceBuilder<ParameterResource> Office365CalendarClientId,
    IResourceBuilder<ParameterResource> Office365CalendarClientSecret,
    IResourceBuilder<ParameterResource> ZoomClientId,
    IResourceBuilder<ParameterResource> ZoomClientSecret
);
