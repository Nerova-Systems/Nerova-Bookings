using AppGateway.Filters;
using Microsoft.OpenApi;
using SharedKernel.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace AppGateway.ApiAggregation;

public class ApiAggregationService(
    ILogger<ApiAggregationService> logger,
    IProxyConfigProvider proxyConfigProvider,
    IHttpClientFactory httpClientFactory,
    PortAllocation ports
)
{
    public async Task<string> GetAggregatedOpenApiJson()
    {
        var openApiDocument = await GetAggregatedOpenApiDocumentAsync();
        var stringWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(stringWriter);
        openApiDocument.SerializeAsV3(jsonWriter);
        return stringWriter.ToString();
    }

    private async Task<OpenApiDocument> GetAggregatedOpenApiDocumentAsync()
    {
        var aggregatedOpenApiDocument = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "PlatformPlatform API", Version = "v1" },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>()
            }
        };

        var proxyConfiguration = proxyConfigProvider.GetConfig();

        // account-api emits two OpenAPI documents (account, back-office) post-consolidation; the
        // user-facing aggregator only surfaces 'account' since back-office endpoints don't appear
        // in the user-facing contract.
        var accountCluster = proxyConfiguration.Clusters.FirstOrDefault(c => c.ClusterId == "account-api");
        if (accountCluster is not null)
        {
            var accountDocument = await FetchOpenApiDocument(accountCluster, "account");
            CombineOpenApiDocuments(aggregatedOpenApiDocument, accountDocument);
        }

        FilterInternalEndpoints(aggregatedOpenApiDocument);

        return aggregatedOpenApiDocument;
    }

    private async Task<OpenApiDocument> FetchOpenApiDocument(ClusterConfig cluster, string documentName)
    {
        // IProxyConfigProvider.GetConfig() returns the unfiltered source config (the appsettings.json
        // placeholder), so resolve the destination ourselves using the same env-var-then-PortAllocation
        // logic as ClusterDestinationConfigFilter.
        var clusterBasePath = ClusterDestinationConfigFilter.ResolveClusterAddress(cluster.ClusterId, ports);

        var clusterOpenApiUrl = $"{clusterBasePath}/openapi/{documentName}.json";
        logger.LogInformation("Fetching OpenAPI document for cluster {ClusterId} from {Url}", cluster.ClusterId, clusterOpenApiUrl);

        using var httpClient = httpClientFactory.CreateClient();

        var response = await httpClient.GetAsync(clusterOpenApiUrl);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var (openApiDocument, _) = await OpenApiDocument.LoadAsync(stream);
        return openApiDocument ?? throw new InvalidOperationException($"Failed to load OpenAPI document from {clusterOpenApiUrl}");
    }

    private void CombineOpenApiDocuments(OpenApiDocument aggregatedOpenApiDocument, OpenApiDocument openApiDocument)
    {
        // Merge paths
        foreach (var path in openApiDocument.Paths)
        {
            if (!aggregatedOpenApiDocument.Paths.ContainsKey(path.Key))
            {
                aggregatedOpenApiDocument.Paths.Add(path.Key, path.Value);
            }
        }

        // Merge schemas
        if (openApiDocument.Components?.Schemas is not null && aggregatedOpenApiDocument.Components?.Schemas is not null)
        {
            foreach (var schema in openApiDocument.Components.Schemas)
            {
                if (aggregatedOpenApiDocument.Components.Schemas.ContainsKey(schema.Key))
                {
                    logger.LogWarning("Duplicate schema found for {SchemaKey}", schema.Key);
                }
                else
                {
                    aggregatedOpenApiDocument.Components.Schemas.Add(schema.Key, schema.Value);
                }
            }
        }

        var serverUrl = openApiDocument.Servers?.FirstOrDefault()?.Url ?? "unknown";
        logger.LogInformation(
            "Successfully fetched and merged OpenAPI document for {ServerUrl}",
            serverUrl
        );
    }

    private static void FilterInternalEndpoints(OpenApiDocument openApiDocument)
    {
        var internalPaths = openApiDocument.Paths
            .Where(p => p.Key.StartsWith("/internal-api/"))
            .Select(p => p.Key)
            .ToArray();

        foreach (var path in internalPaths)
        {
            openApiDocument.Paths.Remove(path);
        }
    }
}
