namespace AppHost;

public static class ConfigurationExtensions
{
    extension<TDestination>(IResourceBuilder<TDestination> builder) where TDestination : IResourceWithEnvironment
    {
        public IResourceBuilder<TDestination> WithUrlConfiguration(string hostname, int gatewayPort, string applicationBasePath)
        {
            // Omit the port for the standard HTTPS/HTTP ports so public, tunnel-fronted URLs (reached on
            // 443) render without a port suffix while local Aspire ports (e.g. 9000) keep theirs.
            var portSuffix = gatewayPort is 443 or 80 ? string.Empty : $":{gatewayPort}";
            var baseUrl = $"https://{hostname}{portSuffix}";
            applicationBasePath = applicationBasePath.TrimEnd('/');

            return builder
                .WithEnvironment("PUBLIC_URL", baseUrl)
                .WithEnvironment("CDN_URL", baseUrl + applicationBasePath);
        }
    }
}
