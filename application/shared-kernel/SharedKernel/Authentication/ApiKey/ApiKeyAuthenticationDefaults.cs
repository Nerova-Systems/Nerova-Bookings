namespace SharedKernel.Authentication.ApiKey;

/// <summary>Constants for the Nerova API key authentication scheme.</summary>
public static class ApiKeyAuthenticationDefaults
{
    /// <summary>The registered name of the API key authentication scheme.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>Prefix shared by all Nerova API key tokens (<c>nerova_</c>).</summary>
    public const string TokenPrefix = "nerova_";
}
