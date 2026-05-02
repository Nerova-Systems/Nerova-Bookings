namespace AppGateway;

public sealed class HostnamesOptions
{
    public const string SectionName = "Hostnames";

    public const string WildcardKey = "*";

    public string App { get; init; } = string.Empty;

    public string[] AllHostnames => [App];

    public string? Resolve(string hostnameKey)
    {
        return hostnameKey switch
        {
            "App" => App,
            _ => null
        };
    }
}
