using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace AppGateway.Filters;

public sealed class HostMatchConfigFilter(IOptions<HostnamesOptions> hostnamesOptions) : IProxyConfigFilter
{
    private const string HostnameKeyMetadata = "HostnameKey";

    private readonly HostnamesOptions _hostnames = hostnamesOptions.Value;

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        return new ValueTask<ClusterConfig>(cluster);
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        if (route.Metadata is null || !route.Metadata.TryGetValue(HostnameKeyMetadata, out var hostnameKey))
        {
            return new ValueTask<RouteConfig>(route);
        }

        var hosts = ResolveHosts(hostnameKey);
        if (hosts is null)
        {
            throw new InvalidOperationException(
                $"Route '{route.RouteId}' declares HostnameKey '{hostnameKey}' which does not match any configured hostname."
            );
        }

        return new ValueTask<RouteConfig>(route with { Match = route.Match with { Hosts = hosts } });
    }

    private string[]? ResolveHosts(string hostnameKey)
    {
        if (hostnameKey == HostnamesOptions.WildcardKey)
        {
            return _hostnames.AllHostnames;
        }

        var hostname = _hostnames.Resolve(hostnameKey);
        return hostname is null ? null : [hostname];
    }
}
