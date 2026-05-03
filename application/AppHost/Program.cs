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

var (payFastConfigured, payFastMerchantId, payFastMerchantKey, payFastPassphrase, payFastSandbox, payFastNotifyUrl, payFastReturnUrl, payFastCancelUrl, payFastAllowedIps) = ConfigurePayFastParameters();
var (
    paystackConfigured,
    paystackPublicKey,
    paystackSecretKey,
    paystackCallbackUrl,
    paystackWebhookUrl,
    paystackAccountCallbackUrl,
    paystackStandardPlanCode,
    paystackPremiumPlanCode) = ConfigurePaystackParameters();
var (twilioConfigured, twilioAccountSid, twilioAuthToken, twilioVerifyServiceSid) = ConfigureTwilioParameters();
var (nangoConfigured, nangoToolboxSecretKey, nangoServerUrl) = ConfigureNangoParameters();

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
    .WithEnvironment("Paystack__SubscriptionEnabled", paystackConfigured ? "true" : "false")
    .WithEnvironment("Paystack__SecretKey", paystackSecretKey)
    .WithEnvironment("Paystack__WebhookSecret", paystackSecretKey)
    .WithEnvironment("Paystack__Plans__Standard__Code", paystackStandardPlanCode)
    .WithEnvironment("Paystack__Plans__Premium__Code", paystackPremiumPlanCode)
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
    .WithEnvironment("PayFast__MerchantId", payFastMerchantId)
    .WithEnvironment("PayFast__MerchantKey", payFastMerchantKey)
    .WithEnvironment("PayFast__Passphrase", payFastPassphrase)
    .WithEnvironment("PayFast__Sandbox", payFastSandbox)
    .WithEnvironment("PayFast__NotifyUrl", payFastNotifyUrl)
    .WithEnvironment("PayFast__ReturnUrl", payFastReturnUrl)
    .WithEnvironment("PayFast__CancelUrl", payFastCancelUrl)
    .WithEnvironment("PayFast__AllowedIps", payFastAllowedIps)
    .WithEnvironment("Paystack__SubscriptionEnabled", paystackConfigured ? "true" : "false")
    .WithEnvironment("Paystack__AllowMockProvider", "true")
    .WithEnvironment("Paystack__SecretKey", paystackSecretKey)
    .WithEnvironment("Paystack__WebhookSecret", paystackSecretKey)
    .WithEnvironment("Paystack__CallbackUrl", paystackAccountCallbackUrl)
    .WithEnvironment("Paystack__Plans__Standard__Code", paystackStandardPlanCode)
    .WithEnvironment("Paystack__Plans__Premium__Code", paystackPremiumPlanCode)
    .WaitFor(accountWorkers);

var mainDatabase = postgres
    .AddDatabase("main-database", "main");

var mainWorkers = builder
    .AddProject<Main_Workers>("main-workers")
    .WithEnvironment("KESTREL_PORT", ports.MainWorkers.ToString())
    .WithReference(mainDatabase)
    .WithReference(azureStorage)
    .WithEnvironment("NANGO_TOOLBOX_SECRET_KEY", nangoToolboxSecretKey)
    .WithEnvironment("NANGO_SECRET_KEY", nangoToolboxSecretKey)
    .WithEnvironment("NANGO_SERVER_URL", nangoServerUrl)
    .WaitFor(mainDatabase);

var mainApi = builder
    .AddProject<Main_Api>("main-api")
    .WithEnvironment("KESTREL_PORT", ports.MainApi.ToString())
    .WithUrlConfiguration(appHostname, ports.AppGateway, "")
    .WithReference(mainDatabase)
    .WithReference(azureStorage)
    .WithEnvironment("PUBLIC_GOOGLE_OAUTH_ENABLED", googleOAuthConfigured ? "true" : "false")
    .WithEnvironment("PUBLIC_SUBSCRIPTION_ENABLED", payFastConfigured ? "true" : "false")
    .WithEnvironment("PUBLIC_PAYSTACK_ENABLED", paystackConfigured ? "true" : "false")
    .WithEnvironment("PAYSTACK_PUBLIC_KEY", paystackPublicKey)
    .WithEnvironment("PAYSTACK_SECRET_KEY", paystackSecretKey)
    .WithEnvironment("PAYSTACK_CALLBACK_URL", paystackCallbackUrl)
    .WithEnvironment("PAYSTACK_WEBHOOK_URL", paystackWebhookUrl)
    .WithEnvironment("PUBLIC_TWILIO_VERIFY_ENABLED", twilioConfigured ? "true" : "false")
    .WithEnvironment("TWILIO_ACCOUNT_SID", twilioAccountSid)
    .WithEnvironment("TWILIO_AUTH_TOKEN", twilioAuthToken)
    .WithEnvironment("TWILIO_MASTER_ACCOUNT_SID", twilioAccountSid)
    .WithEnvironment("TWILIO_MASTER_AUTH_TOKEN", twilioAuthToken)
    .WithEnvironment("TWILIO_VERIFY_SERVICE_SID", twilioVerifyServiceSid)
    .WithEnvironment("PUBLIC_NANGO_ENABLED", nangoConfigured ? "true" : "false")
    .WithEnvironment("NANGO_TOOLBOX_SECRET_KEY", nangoToolboxSecretKey)
    .WithEnvironment("NANGO_SECRET_KEY", nangoToolboxSecretKey)
    .WithEnvironment("NANGO_SERVER_URL", nangoServerUrl)
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

(bool Configured,
    IResourceBuilder<ParameterResource> MerchantId,
    IResourceBuilder<ParameterResource> MerchantKey,
    IResourceBuilder<ParameterResource> Passphrase,
    IResourceBuilder<ParameterResource> Sandbox,
    IResourceBuilder<ParameterResource> NotifyUrl,
    IResourceBuilder<ParameterResource> ReturnUrl,
    IResourceBuilder<ParameterResource> CancelUrl,
    IResourceBuilder<ParameterResource> AllowedIps) ConfigurePayFastParameters()
{
    _ = builder.AddParameter("payfast-enabled")
        .WithDescription("""
                         **PayFast Integration** -- Enables subscription billing via PayFast tokenization (Type 2).

                         **Important**: Set up a [PayFast sandbox account](https://sandbox.payfast.co.za) and configure credentials according to the guide in README.md **before** enabling this.

                         - Enter `true` to enable PayFast, or `false` to skip. This can be changed later.
                         - After enabling, **restart Aspire** to be prompted for Merchant ID and Merchant Key.

                         See **README.md** for full setup instructions.
                         """, true
        );

    var configured = builder.Configuration["Parameters:payfast-enabled"] == "true";

    if (configured)
    {
        var merchantId = builder.AddParameter("payfast-merchant-id", true)
            .WithDescription("PayFast Merchant ID from the [PayFast Dashboard](https://www.payfast.co.za/merchant).", true);
        var merchantKey = builder.AddParameter("payfast-merchant-key", true)
            .WithDescription("PayFast Merchant Key from the [PayFast Dashboard](https://www.payfast.co.za/merchant).", true);
        var passphrase = builder.AddParameter("payfast-passphrase", true)
            .WithDescription("PayFast Passphrase set in the [PayFast Dashboard](https://www.payfast.co.za/merchant) under Security.", true);
        var sandbox = builder.AddParameter("payfast-sandbox", true)
            .WithDescription("Set to `true` for sandbox/testing or `false` for live payments.", true);
        var notifyUrl = builder.AddParameter("payfast-notify-url", true)
            .WithDescription("ITN callback URL — must be publicly reachable by PayFast (e.g. ngrok URL in development).", true);
        var returnUrl = builder.AddParameter("payfast-return-url", true)
            .WithDescription("URL PayFast redirects to after a successful payment.", true);
        var cancelUrl = builder.AddParameter("payfast-cancel-url", true)
            .WithDescription("URL PayFast redirects to when a payment is cancelled.", true);
        var allowedIps = builder.AddParameter("payfast-allowed-ips", true)
            .WithDescription("Comma-separated list of PayFast ITN IP addresses allowed to post callbacks.", true);

        return (configured, merchantId, merchantKey, passphrase, sandbox, notifyUrl, returnUrl, cancelUrl, allowedIps);
    }

    return (
        configured,
        builder.CreateResourceBuilder(new ParameterResource("payfast-merchant-id", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-merchant-key", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-passphrase", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-sandbox", _ => "true", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-notify-url", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-return-url", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-cancel-url", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("payfast-allowed-ips", _ => "not-configured", true))
    );
}

(bool Configured,
    IResourceBuilder<ParameterResource> PublicKey,
    IResourceBuilder<ParameterResource> SecretKey,
    IResourceBuilder<ParameterResource> CallbackUrl,
    IResourceBuilder<ParameterResource> WebhookUrl,
    IResourceBuilder<ParameterResource> AccountCallbackUrl,
    IResourceBuilder<ParameterResource> StandardPlanCode,
    IResourceBuilder<ParameterResource> PremiumPlanCode) ConfigurePaystackParameters()
{
    _ = builder.AddParameter("paystack-enabled")
        .WithDescription("Enable Paystack for account subscriptions and client appointment deposits/payments. Set to `true` after test keys are available.", true);

    var configured = builder.Configuration["Parameters:paystack-enabled"] == "true";

    if (configured)
    {
        var publicKey = builder.AddParameter("paystack-public-key", true)
            .WithDescription("Paystack test public key used by public appointment payment screens.", true);
        var secretKey = builder.AddParameter("paystack-secret-key", true)
            .WithDescription("Paystack test secret key used for server-side transaction verification and webhook signatures.", true);
        var callbackUrl = builder.CreateResourceBuilder(
            new ParameterResource(
                "paystack-callback-url",
                _ => builder.Configuration["Parameters:paystack-callback-url"] ?? $"{appBaseUrl}/book/payment/callback",
                true
            )
        );
        var webhookUrl = builder.CreateResourceBuilder(
            new ParameterResource(
                "paystack-webhook-url",
                _ => builder.Configuration["Parameters:paystack-webhook-url"] ?? "https://localhost:9000/api/main/payments/paystack/webhook",
                true
            )
        );
        var accountCallbackUrl = builder.CreateResourceBuilder(
            new ParameterResource(
                "paystack-account-callback-url",
                _ => builder.Configuration["Parameters:paystack-account-callback-url"] ?? $"{appBaseUrl}/account/billing/subscription",
                true
            )
        );
        var standardPlanCode = builder.AddParameter("paystack-standard-plan-code", true)
            .WithDescription("Paystack plan code used for Standard account subscriptions.", true);
        var premiumPlanCode = builder.AddParameter("paystack-premium-plan-code", true)
            .WithDescription("Paystack plan code used for Premium account subscriptions.", true);

        return (configured, publicKey, secretKey, callbackUrl, webhookUrl, accountCallbackUrl, standardPlanCode, premiumPlanCode);
    }

    return (
        configured,
        builder.CreateResourceBuilder(new ParameterResource("paystack-public-key", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-secret-key", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-callback-url", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-webhook-url", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-account-callback-url", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-standard-plan-code", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("paystack-premium-plan-code", _ => "not-configured", true))
    );
}

(bool Configured,
    IResourceBuilder<ParameterResource> AccountSid,
    IResourceBuilder<ParameterResource> AuthToken,
    IResourceBuilder<ParameterResource> VerifyServiceSid) ConfigureTwilioParameters()
{
    _ = builder.AddParameter("twilio-verify-enabled")
        .WithDescription("Enable Twilio Verify SMS OTP for public booking phone verification. Set to `true` after Verify credentials are configured.", true);

    var accountSidFromEnvironment = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
    var authTokenFromEnvironment = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
    var verifyServiceSidFromEnvironment = Environment.GetEnvironmentVariable("TWILIO_VERIFY_SERVICE_SID");
    var masterAccountSidFromEnvironment = Environment.GetEnvironmentVariable("TWILIO_MASTER_ACCOUNT_SID");
    var masterAuthTokenFromEnvironment = Environment.GetEnvironmentVariable("TWILIO_MASTER_AUTH_TOKEN");
    accountSidFromEnvironment = string.IsNullOrWhiteSpace(masterAccountSidFromEnvironment) ? accountSidFromEnvironment : masterAccountSidFromEnvironment;
    authTokenFromEnvironment = string.IsNullOrWhiteSpace(masterAuthTokenFromEnvironment) ? authTokenFromEnvironment : masterAuthTokenFromEnvironment;
    var configured = builder.Configuration["Parameters:twilio-verify-enabled"] == "true" ||
                     (!string.IsNullOrWhiteSpace(accountSidFromEnvironment) &&
                      !string.IsNullOrWhiteSpace(authTokenFromEnvironment) &&
                      !string.IsNullOrWhiteSpace(verifyServiceSidFromEnvironment));
    var whatsAppConfigured = builder.Configuration["Parameters:twilio-whatsapp-enabled"] == "true" ||
                             (!string.IsNullOrWhiteSpace(accountSidFromEnvironment) &&
                              !string.IsNullOrWhiteSpace(authTokenFromEnvironment));

    _ = builder.AddParameter("twilio-whatsapp-enabled")
        .WithDescription("Enable Twilio tenant WhatsApp provisioning. This uses platform/master Twilio credentials; tenant senders are assigned and approved per business.", true);

    if (configured || whatsAppConfigured)
    {
        var accountSid = string.IsNullOrWhiteSpace(accountSidFromEnvironment)
            ? builder.AddParameter("twilio-account-sid", true).WithDescription("Twilio Account SID used for Verify SMS and WhatsApp messaging.", true)
            : builder.CreateResourceBuilder(new ParameterResource("twilio-account-sid", _ => accountSidFromEnvironment, true));
        var authToken = string.IsNullOrWhiteSpace(authTokenFromEnvironment)
            ? builder.AddParameter("twilio-auth-token", true).WithDescription("Twilio Auth Token used for Verify SMS and WhatsApp messaging.", true)
            : builder.CreateResourceBuilder(new ParameterResource("twilio-auth-token", _ => authTokenFromEnvironment, true));
        var verifyServiceSid = configured && string.IsNullOrWhiteSpace(verifyServiceSidFromEnvironment)
            ? builder.AddParameter("twilio-verify-service-sid", true)
                .WithDescription("Twilio Verify Service SID for public booking OTP checks.", true)
            : builder.CreateResourceBuilder(new ParameterResource("twilio-verify-service-sid", _ => string.IsNullOrWhiteSpace(verifyServiceSidFromEnvironment) ? "not-configured" : verifyServiceSidFromEnvironment, true));

        return (configured || whatsAppConfigured, accountSid, authToken, verifyServiceSid);
    }

    return (
        configured,
        builder.CreateResourceBuilder(new ParameterResource("twilio-account-sid", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("twilio-auth-token", _ => "not-configured", true)),
        builder.CreateResourceBuilder(new ParameterResource("twilio-verify-service-sid", _ => "not-configured", true))
    );
}

(bool Configured,
    IResourceBuilder<ParameterResource> ToolboxSecretKey,
    IResourceBuilder<ParameterResource> ServerUrl) ConfigureNangoParameters()
{
    _ = builder.AddParameter("nango-enabled")
        .WithDescription("""
                         **Nango Connectors** -- Enables Nango-backed connector setup, starting with Google Calendar.

                         **Important**: Create the `google-calendar` integration in Nango before enabling this.

                         - Enter `true` to enable Nango connector flows, or `false` to skip.
                         - After enabling, restart Aspire to be prompted for the Nango secret key.

                         The server URL defaults to Nango Cloud: `https://api.nango.dev`.
                         """, true);

    var secretFromEnvironment = Environment.GetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY");
    if (string.IsNullOrWhiteSpace(secretFromEnvironment))
    {
        secretFromEnvironment = Environment.GetEnvironmentVariable("NANGO_SECRET_KEY");
    }

    var serverUrlFromEnvironment = Environment.GetEnvironmentVariable("NANGO_SERVER_URL");
    var configured = builder.Configuration["Parameters:nango-enabled"] == "true" ||
                     !string.IsNullOrWhiteSpace(secretFromEnvironment);

    var serverUrl = string.IsNullOrWhiteSpace(serverUrlFromEnvironment)
        ? builder.CreateResourceBuilder(
            new ParameterResource(
                "nango-server-url",
                _ => builder.Configuration["Parameters:nango-server-url"] ?? "https://api.nango.dev",
                true
            )
        )
        : builder.CreateResourceBuilder(new ParameterResource("nango-server-url", _ => serverUrlFromEnvironment, true));

    if (configured)
    {
        var toolboxSecretKey = string.IsNullOrWhiteSpace(secretFromEnvironment)
            ? builder.AddParameter("nango-toolbox-secret-key", true)
                .WithDescription("Nango secret key used to create connect sessions and read connection metadata.", true)
            : builder.CreateResourceBuilder(new ParameterResource("nango-toolbox-secret-key", _ => secretFromEnvironment, true));

        return (configured, toolboxSecretKey, serverUrl);
    }

    return (
        configured,
        builder.CreateResourceBuilder(new ParameterResource("nango-toolbox-secret-key", _ => "not-configured", true)),
        serverUrl
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
