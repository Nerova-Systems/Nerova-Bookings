namespace Main.Integrations.Meta;

/// <summary>
///     Deterministic in-memory Meta Graph client used for local development (no Meta secrets configured)
///     and tests. Returns stable fake data so the full onboarding flow can be exercised without contacting
///     Meta. The returned ids echo the caller-supplied ids so assertions remain stable across runs.
/// </summary>
public sealed class MockMetaGraphClient : IMetaGraphClient
{
    public const string MockAccessToken = "mock-whatsapp-access-token";
    public const string MockBusinessName = "Mock WhatsApp Business";
    public const string MockVerifiedName = "Mock Verified Business";
    public const string MockDisplayPhoneNumber = "+1 555-0100";

    public Task<string?> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(MockAccessToken);
    }

    public Task<bool> RegisterPhoneNumberAsync(string phoneNumberId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task<bool> SubscribeAppToWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task<MetaWabaMetadata?> GetWabaAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult<MetaWabaMetadata?>(new MetaWabaMetadata(wabaId, MockBusinessName));
    }

    public Task<MetaPhoneNumber[]?> GetPhoneNumbersAsync(string wabaId, string accessToken, CancellationToken cancellationToken)
    {
        return Task.FromResult<MetaPhoneNumber[]?>([new MetaPhoneNumber($"{wabaId}-phone", MockDisplayPhoneNumber, MockVerifiedName)]);
    }

    public Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string toPhoneNumber, string text, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>($"wamid.MOCK_{Guid.NewGuid():N}");
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
        return Task.FromResult<string?>($"wamid.MOCK_{Guid.NewGuid():N}");
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
        return Task.FromResult<string?>($"wamid.MOCK_{Guid.NewGuid():N}");
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
        return Task.FromResult<string?>($"wamid.MOCK_{Guid.NewGuid():N}");
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
        return Task.FromResult<string?>($"wamid.MOCK_{Guid.NewGuid():N}");
    }

    public Task<string?> CreateAndPublishFlowAsync(string wabaId, string flowName, string category, string flowJson, string accessToken, CancellationToken cancellationToken)
        => Task.FromResult<string?>($"mock-flow-{flowName.ToLowerInvariant().Replace(' ', '-')}");

    public Task<bool> UpdateFlowJsonAsync(string flowId, string flowJson, string accessToken, CancellationToken cancellationToken)
        => Task.FromResult(true);

    public Task<bool> UploadFlowPublicKeyAsync(string wabaId, string publicKeyPem, string accessToken, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
