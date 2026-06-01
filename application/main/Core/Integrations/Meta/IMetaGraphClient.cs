namespace Main.Integrations.Meta;

public interface IMetaGraphClient
{
    /// <summary>
    ///     Exchanges a Meta Embedded Signup authorization code for a long-lived business access token.
    ///     Returns null when the exchange fails so callers can surface a clean error without exception handling.
    /// </summary>
    Task<string?> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    ///     Registers a phone number for Cloud API usage with a freshly generated 6-digit PIN.
    ///     Returns false on any failure.
    /// </summary>
    Task<bool> RegisterPhoneNumberAsync(string phoneNumberId, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Subscribes the platform app to the WhatsApp Business Account so it receives webhooks.
    ///     Returns false on any failure.
    /// </summary>
    Task<bool> SubscribeAppToWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves WhatsApp Business Account metadata. Returns null on any failure.
    /// </summary>
    Task<MetaWabaMetadata?> GetWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves the phone numbers registered under a WhatsApp Business Account. Returns null on any failure.
    /// </summary>
    Task<MetaPhoneNumber[]?> GetPhoneNumbersAsync(string wabaId, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Sends a text message via the WhatsApp Cloud API.
    ///     Returns the Meta message ID (wamid.*) on success, or null on any failure.
    /// </summary>
    Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string toPhoneNumber, string text, CancellationToken cancellationToken);
}

public sealed record MetaWabaMetadata(string Id, string Name);

public sealed record MetaPhoneNumber(string Id, string DisplayPhoneNumber, string VerifiedName);
