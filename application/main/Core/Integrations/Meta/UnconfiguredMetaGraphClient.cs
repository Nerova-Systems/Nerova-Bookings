namespace Main.Integrations.Meta;

/// <summary>
///     Sentinel Meta client used in non-development environments when the real Meta credentials are
///     not configured. It never pretends to be live, so calls fail cleanly and the app can surface
///     the real configuration problem instead of silently using the mock provider.
/// </summary>
public sealed class UnconfiguredMetaGraphClient : IMetaGraphClient
{
    public Task<string?> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> RegisterPhoneNumberAsync(string phoneNumberId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<bool> SubscribeAppToWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<MetaWabaMetadata?> GetWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult<MetaWabaMetadata?>(null);
    }

    public Task<MetaPhoneNumber[]?> GetPhoneNumbersAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult<MetaPhoneNumber[]?>(null);
    }

    public Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string toPhoneNumber, string text, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> SendInteractiveButtonsAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        IReadOnlyList<WhatsAppReplyButton> buttons,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> SendInteractiveListAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string buttonLabel,
        IReadOnlyList<WhatsAppListSection> sections,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> SendCtaUrlButtonAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneNumber,
        string bodyText,
        string buttonText,
        string url,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> SendFlowMessageAsync(
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
    )
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> CreateAndPublishFlowAsync(string wabaId, string flowName, string category, string flowJson, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> UpdateFlowJsonAsync(string flowId, string flowJson, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<bool> UploadFlowPublicKeyAsync(string wabaId, string publicKeyPem, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<MetaFlowInfo[]?> ListFlowsAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult<MetaFlowInfo[]?>(null);
    }
}
