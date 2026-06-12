namespace Main.Integrations.Ai;

/// <summary>
///     Configuration for the AI Front Desk model access (docs/agentic-system-spec.md §6.1). Claude is
///     reached through the Anthropic Messages API shape, which both api.anthropic.com and the Microsoft
///     Foundry Anthropic endpoint expose — switching providers is a configuration change, not a code change.
///     When no API key is configured (local dev, tests), the deterministic <see cref="ScriptedChatClient" />
///     is used instead so the full pipeline stays exercisable without a live model.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Base address of the Anthropic-compatible endpoint (api.anthropic.com or a Foundry resource).</summary>
    public string Endpoint { get; set; } = "https://api.anthropic.com";

    public string? ApiKey { get; set; }

    /// <summary>Default model for receptionist turns and import inference (Sonnet-class).</summary>
    public string Model { get; set; } = "claude-sonnet-4-5";

    /// <summary>Cheaper model for classification, copy, and memory distillation paths (Haiku-class).</summary>
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
}
