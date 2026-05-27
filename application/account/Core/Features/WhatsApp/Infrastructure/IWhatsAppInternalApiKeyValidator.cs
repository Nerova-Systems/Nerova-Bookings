namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Validates the internal API key header value (<c>Authorization: ApiKey &lt;key&gt;</c>) that
///     guards server-to-server calls from the main SCS into the WhatsApp internal endpoints.
///     The expected key is read from <c>WhatsApp:InternalApiKey</c> in configuration.
/// </summary>
public interface IWhatsAppInternalApiKeyValidator
{
    bool IsValid(string? authorizationHeader);
}
