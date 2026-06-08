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

    /// <summary>
    ///     Sends an interactive reply-buttons message (1-3 buttons) via the WhatsApp Cloud API. Button titles
    ///     are limited by Meta to 20 characters. Returns the Meta message ID (wamid.*) on success, or null on any failure.
    /// </summary>
    Task<string?> SendInteractiveButtonsAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        IReadOnlyList<WhatsAppReplyButton> buttons,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     Sends an interactive single-select list message (up to 10 rows total across sections) via the WhatsApp
    ///     Cloud API. Returns the Meta message ID (wamid.*) on success, or null on any failure.
    /// </summary>
    Task<string?> SendInteractiveListAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string buttonLabel,
        IReadOnlyList<WhatsAppListSection> sections,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     Sends an interactive call-to-action URL button message via the WhatsApp Cloud API. Used to deliver a
    ///     tappable link (for example a Paystack checkout URL). Returns the Meta message ID (wamid.*) on success, or null on any failure.
    /// </summary>
    Task<string?> SendCtaUrlButtonAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string buttonText,
        string url,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     Sends an interactive WhatsApp Flow message via the Cloud API. <paramref name="flowToken" /> is an
    ///     opaque correlation token echoed back in the flow-completion (nfm_reply) webhook so the conversation
    ///     can be matched. When <paramref name="initialScreen" /> is provided the flow opens with
    ///     <c>navigate</c> to that screen (optionally seeded with <paramref name="initialData" />); otherwise it
    ///     opens with <c>data_exchange</c> and the data endpoint serves the first screen. Returns the Meta
    ///     message ID (wamid.*) on success, or null on any failure.
    /// </summary>
    Task<string?> SendFlowMessageAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string flowId,
        string flowToken,
        string flowCtaText,
        string? initialScreen,
        object? initialData,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     Creates a new WhatsApp Flow under the given WABA and uploads its JSON definition.
    ///     Returns the new flow ID on success, or null on any failure.
    /// </summary>
    Task<string?> CreateAndPublishFlowAsync(string wabaId, string flowName, string category, string flowJson, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Uploads a new Flow JSON definition to an existing flow (replaces the current asset).
    ///     Used to update flows that were previously created with the wrong JSON.
    ///     Returns true on success.
    /// </summary>
    Task<bool> UpdateFlowJsonAsync(string flowId, string flowJson, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Uploads the platform RSA public key to a WABA's Flows data endpoint configuration so Meta can
    ///     encrypt data-exchange requests. Returns true on success.
    /// </summary>
    Task<bool> UploadFlowPublicKeyAsync(string wabaId, string publicKeyPem, string accessToken, CancellationToken cancellationToken);
}

public sealed record MetaWabaMetadata(string Id, string Name);

public sealed record MetaPhoneNumber(string Id, string DisplayPhoneNumber, string VerifiedName);

/// <summary>A tappable quick-reply button in an interactive message. <paramref name="Id" /> is echoed back in the inbound webhook when tapped.</summary>
public sealed record WhatsAppReplyButton(string Id, string Title);

/// <summary>A selectable row in an interactive list message. <paramref name="Id" /> is echoed back in the inbound webhook when selected.</summary>
public sealed record WhatsAppListRow(string Id, string Title, string? Description = null);

/// <summary>A titled group of <see cref="WhatsAppListRow" /> entries in an interactive list message.</summary>
public sealed record WhatsAppListSection(string Title, IReadOnlyList<WhatsAppListRow> Rows);
