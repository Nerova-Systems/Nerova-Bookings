namespace Main.Integrations.Ai;

/// <summary>
///     Model provider for the AI Front Desk. <see cref="Scripted" /> is the deterministic no-model default
///     used by tests and unconfigured environments; <see cref="AzureOpenAi" /> targets an Azure OpenAI
///     resource (deployment names as models); <see cref="Anthropic" /> targets the Anthropic Messages API
///     (api.anthropic.com or a Foundry `/anthropic` endpoint) — the planned later switch for Claude.
/// </summary>
public enum AiProvider
{
    Scripted,
    AzureOpenAi,
    Anthropic
}

/// <summary>
///     Configuration for the AI Front Desk model access (docs/agentic-system-spec.md §6.1). The provider is
///     a configuration switch: Azure OpenAI today, Claude through the same Anthropic Messages API shape on
///     api.anthropic.com or Microsoft Foundry later — no code change required. When nothing is configured
///     (local dev, tests), the deterministic <see cref="ScriptedChatClient" /> is used instead so the full
///     pipeline stays exercisable without a live model.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Explicit provider selection: "AzureOpenAI", "Anthropic", or "Scripted". Empty = infer from ApiKey.</summary>
    public string Provider { get; set; } = "";

    /// <summary>Base address of the provider endpoint (Azure OpenAI resource, api.anthropic.com, or a Foundry resource).</summary>
    public string Endpoint { get; set; } = "https://api.anthropic.com";

    public string? ApiKey { get; set; }

    /// <summary>Default model for receptionist turns and import inference (Sonnet-class). Azure OpenAI: the deployment name.</summary>
    public string Model { get; set; } = "claude-sonnet-4-5";

    /// <summary>
    ///     Cheaper model for classification, copy, and memory distillation paths (Haiku-class). Azure OpenAI: the
    ///     deployment name.
    /// </summary>
    public string FastModel { get; set; } = "claude-haiku-4-5";

    /// <summary>Hard cap on tool invocations within a single receptionist turn (spec R9).</summary>
    public int MaxToolCallsPerTurn { get; set; } = 10;

    /// <summary>Hard cap on model output tokens per turn.</summary>
    public int MaxOutputTokensPerTurn { get; set; } = 1024;

    /// <summary>Combined input+output token budget per conversation session; breach auto-escalates.</summary>
    public long MaxTokensPerSession { get; set; } = 200_000;

    /// <summary>Combined token budget per tenant per calendar month; breach auto-escalates.</summary>
    public long MaxTokensPerTenantPerMonth { get; set; } = 10_000_000;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    ///     Clamps budgets to sane operating ranges so a bad environment value can never disable the
    ///     spend guardrails (e.g. a zero or negative cap silently turning budgets off).
    /// </summary>
    public void ClampBudgets()
    {
        MaxToolCallsPerTurn = Math.Clamp(MaxToolCallsPerTurn, 1, 50);
        MaxOutputTokensPerTurn = Math.Clamp(MaxOutputTokensPerTurn, 64, 32_768);
        MaxTokensPerSession = Math.Clamp(MaxTokensPerSession, 1_000, 10_000_000);
        MaxTokensPerTenantPerMonth = Math.Clamp(MaxTokensPerTenantPerMonth, 10_000, 1_000_000_000);
    }

    /// <summary>
    ///     Resolves the effective provider. An explicit <see cref="Provider" /> wins; otherwise an API key
    ///     implies Anthropic (pre-provider behavior preserved), and no key means Scripted. A provider that
    ///     is missing its API key always degrades to Scripted — the deterministic Flows engine remains the
    ///     booking fallback either way (spec reliability rule).
    /// </summary>
    public AiProvider ResolveProvider()
    {
        if (!IsConfigured) return AiProvider.Scripted;

        return Provider.ToLowerInvariant() switch
        {
            "azureopenai" => AiProvider.AzureOpenAi,
            "anthropic" => AiProvider.Anthropic,
            "scripted" => AiProvider.Scripted,
            _ => AiProvider.Anthropic
        };
    }
}
