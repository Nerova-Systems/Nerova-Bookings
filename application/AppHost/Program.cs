using System.Net;
using System.Net.Sockets;
using AppHost;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Projects;

// Check for port conflicts before starting
CheckPortAvailability();

var builder = DistributedApplication.CreateBuilder(args);

var certificatePassword = await builder.CreateSslCertificateIfNotExists();

SecretManagerHelper.GenerateAuthenticationTokenSigningKey("authentication-token-signing-key");

var (googleOAuthConfigured, googleOAuthClientId, googleOAuthClientSecret) = ConfigureGoogleOAuthParameters();

var (payFastConfigured, payFastMerchantId, payFastMerchantKey, payFastPassphrase, payFastSandbox, payFastNotifyUrl, payFastReturnUrl, payFastCancelUrl, payFastAllowedIps) = ConfigurePayFastParameters();

var postgresPassword = builder.CreateStablePassword("postgres-password");
var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 9002)
    .WithDataVolume("platform-platform-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithArgs("-c", "wal_level=logical");

var azureStorage = builder
    .AddAzureStorage("azure-storage")
    .RunAsEmulator(resourceBuilder =>
        {
            resourceBuilder.WithDataVolume("platform-platform-azure-storage-data");
            resourceBuilder.WithBlobPort(10000);
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
    .AddBlobs("blobs");

var messaging = builder
    .AddAzureServiceBus("messaging")
    .RunAsEmulator();

messaging.AddServiceBusTopic("tenant-catalog-upserted").AddServiceBusSubscription("back-office-tenant-catalog-upserted");
messaging.AddServiceBusTopic("tenant-catalog-deleted").AddServiceBusSubscription("back-office-tenant-catalog-deleted");
messaging.AddServiceBusTopic("user-catalog-upserted").AddServiceBusSubscription("back-office-user-catalog-upserted");
messaging.AddServiceBusTopic("user-catalog-deleted").AddServiceBusSubscription("back-office-user-catalog-deleted");

builder
    .AddContainer("mail-server", "axllent/mailpit")
    .WithHttpEndpoint(9003, 8025)
    .WithEndpoint(9004, 1025)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithUrlForEndpoint("http", u => u.DisplayText = "Read mail here");

CreateBlobContainer("avatars");
CreateBlobContainer("logos");

var frontendBuild = builder
    .AddJavaScriptApp("frontend-build", "../")
    .WithEnvironment("CERTIFICATE_PASSWORD", certificatePassword);

var accountDatabase = postgres
    .AddDatabase("account-database", "account");

var accountWorkers = builder
    .AddProject<Account_Workers>("account-workers")
    .WithReference(accountDatabase)
    .WithReference(azureStorage)
    .WithReference(messaging)
    .WaitFor(accountDatabase);

var accountApi = builder
    .AddProject<Account_Api>("account-api")
    .WithUrlConfiguration("/account")
    .WithReference(accountDatabase)
    .WithReference(azureStorage)
    .WithReference(messaging)
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
    .WaitFor(accountWorkers);

var backOfficeDatabase = postgres
    .AddDatabase("back-office-database", "back-office");

var backOfficeWorkers = builder
    .AddProject<BackOffice_Workers>("back-office-workers")
    .WithReference(backOfficeDatabase)
    .WithReference(azureStorage)
    .WithReference(messaging)
    .WaitFor(backOfficeDatabase);

var backOfficeApi = builder
    .AddProject<BackOffice_Api>("back-office-api")
    .WithUrlConfiguration("/back-office")
    .WithReference(backOfficeDatabase)
    .WithReference(azureStorage)
    .WithReference(messaging)
    .WaitFor(backOfficeWorkers);

var mainDatabase = postgres
    .AddDatabase("main-database", "main");

var mainWorkers = builder
    .AddProject<Main_Workers>("main-workers")
    .WithReference(mainDatabase)
    .WithReference(azureStorage)
    .WaitFor(mainDatabase);

var mainApi = builder
    .AddProject<Main_Api>("main-api")
    .WithUrlConfiguration("")
    .WithReference(mainDatabase)
    .WithReference(azureStorage)
    .WithEnvironment("PUBLIC_GOOGLE_OAUTH_ENABLED", googleOAuthConfigured ? "true" : "false")
    .WithEnvironment("PUBLIC_SUBSCRIPTION_ENABLED", payFastConfigured ? "true" : "false")
    .WaitFor(mainWorkers);

var appGateway = builder
    .AddProject<AppGateway>("app-gateway")
    .WithReference(frontendBuild)
    .WithReference(accountApi)
    .WithReference(backOfficeApi)
    .WithReference(mainApi)
    .WaitFor(accountApi)
    .WaitFor(frontendBuild)
    .WithUrlForEndpoint("https", url => url.DisplayText = "Web App");

appGateway.WithUrl($"{appGateway.GetEndpoint("https")}/back-office", "Back Office");
appGateway.WithUrl($"{appGateway.GetEndpoint("https")}/openapi", "Open API");

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

void CreateBlobContainer(string containerName)
{
    var connectionString = builder.Configuration.GetConnectionString("blob-storage");

    new Task(() =>
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            containerClient.CreateIfNotExists();
        }
    ).Start();
}

void CheckPortAvailability()
{
    var ports = new[] { (9098, "Resource Service"), (9097, "Dashboard"), (9001, "Aspire") };
    var blocked = ports.Where(p => !IsPortAvailable(p.Item1)).ToList();

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
