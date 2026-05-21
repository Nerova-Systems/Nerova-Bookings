namespace Account.Features.ApiKeys.Domain;

/// <summary>Defines the scope of a Nerova API key.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiKeyScope
{
    /// <summary>Key is scoped to a personal (solo) tenant and authenticates as the key's owner.</summary>
    User = 0,

    /// <summary>Key is scoped to an organisation and authenticates as the creator in that org context.</summary>
    Organization = 1
}
