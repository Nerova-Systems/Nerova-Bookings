using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Main.Integrations.Ai;

/// <summary>
///     <see cref="IChatClient" /> over the Anthropic Messages API (the wire format Claude exposes on both
///     api.anthropic.com and Microsoft Foundry's Anthropic endpoint). Supports system instructions, typed
///     tool definitions (tool_use / tool_result blocks), and usage accounting. Non-streaming by design:
///     receptionist turns deliver one WhatsApp message per turn, so streaming buys nothing.
/// </summary>
public sealed class AnthropicChatClient(HttpClient httpClient, IOptions<AiOptions> options, ILogger<AnthropicChatClient> logger) : IChatClient
{
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly AiOptions _options = options.Value;

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null, CancellationToken cancellationToken = default)
    {
        var payload = BuildRequestPayload(messages, chatOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.Endpoint), "/v1/messages"));
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Anthropic Messages API returned {StatusCode}: {Body}", (int)response.StatusCode, responseBody);
            throw new HttpRequestException($"Anthropic Messages API returned {(int)response.StatusCode}.");
        }

        return ParseResponse(responseBody);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, chatOptions, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // HttpClient lifetime is owned by IHttpClientFactory.
    }

    private Dictionary<string, object?> BuildRequestPayload(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions)
    {
        var systemSegments = new List<string>();
        if (!string.IsNullOrWhiteSpace(chatOptions?.Instructions))
        {
            systemSegments.Add(chatOptions.Instructions);
        }

        var apiMessages = new List<Dictionary<string, object?>>();
        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                if (!string.IsNullOrWhiteSpace(message.Text)) systemSegments.Add(message.Text);
                continue;
            }

            var blocks = BuildContentBlocks(message);
            if (blocks.Count == 0) continue;

            // Anthropic has no "tool" role: tool results travel as user-role tool_result blocks.
            var role = message.Role == ChatRole.Assistant ? "assistant" : "user";
            apiMessages.Add(new Dictionary<string, object?> { ["role"] = role, ["content"] = blocks });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = chatOptions?.ModelId ?? _options.Model,
            ["max_tokens"] = chatOptions?.MaxOutputTokens ?? _options.MaxOutputTokensPerTurn,
            ["messages"] = apiMessages
        };

        if (systemSegments.Count > 0) payload["system"] = string.Join("\n\n", systemSegments);
        if (chatOptions?.Temperature is not null) payload["temperature"] = chatOptions.Temperature;

        var functions = chatOptions?.Tools?.OfType<AIFunction>().ToArray() ?? [];
        if (functions.Length > 0)
        {
            payload["tools"] = functions.Select(function => new Dictionary<string, object?>
                {
                    ["name"] = function.Name,
                    ["description"] = function.Description,
                    ["input_schema"] = function.JsonSchema
                }
            ).ToArray();
        }

        return payload;
    }

    private static List<Dictionary<string, object?>> BuildContentBlocks(ChatMessage message)
    {
        var blocks = new List<Dictionary<string, object?>>();
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent { Text.Length: > 0 } text:
                    blocks.Add(new Dictionary<string, object?> { ["type"] = "text", ["text"] = text.Text });
                    break;
                case FunctionCallContent functionCall:
                    blocks.Add(new Dictionary<string, object?>
                        {
                            ["type"] = "tool_use",
                            ["id"] = functionCall.CallId,
                            ["name"] = functionCall.Name,
                            ["input"] = functionCall.Arguments ?? new Dictionary<string, object?>()
                        }
                    );
                    break;
                case FunctionResultContent functionResult:
                    blocks.Add(new Dictionary<string, object?>
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = functionResult.CallId,
                            ["content"] = functionResult.Result switch
                            {
                                null => string.Empty,
                                string text => text,
                                JsonElement json => json.GetRawText(),
                                var other => JsonSerializer.Serialize(other, JsonOptions)
                            }
                        }
                    );
                    break;
            }
        }

        return blocks;
    }

    private static ChatResponse ParseResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        var contents = new List<AIContent>();
        if (root.TryGetProperty("content", out var contentBlocks))
        {
            foreach (var block in contentBlocks.EnumerateArray())
            {
                var blockType = block.GetProperty("type").GetString();
                if (blockType == "text")
                {
                    contents.Add(new TextContent(block.GetProperty("text").GetString() ?? string.Empty));
                }
                else if (blockType == "tool_use")
                {
                    var arguments = block.TryGetProperty("input", out var input)
                        ? JsonSerializer.Deserialize<Dictionary<string, object?>>(input.GetRawText())
                        : new Dictionary<string, object?>();
                    contents.Add(new FunctionCallContent(
                            block.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                            block.GetProperty("name").GetString() ?? string.Empty,
                            arguments
                        )
                    );
                }
            }
        }

        var responseMessage = new ChatMessage(ChatRole.Assistant, contents);
        var chatResponse = new ChatResponse(responseMessage)
        {
            ResponseId = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            ModelId = root.TryGetProperty("model", out var model) ? model.GetString() : null,
            FinishReason = root.TryGetProperty("stop_reason", out var stopReason)
                ? stopReason.GetString() switch
                {
                    "tool_use" => ChatFinishReason.ToolCalls,
                    "max_tokens" => ChatFinishReason.Length,
                    _ => ChatFinishReason.Stop
                }
                : ChatFinishReason.Stop
        };

        if (root.TryGetProperty("usage", out var usage))
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = usage.TryGetProperty("input_tokens", out var inputTokens) ? inputTokens.GetInt64() : null,
                OutputTokenCount = usage.TryGetProperty("output_tokens", out var outputTokens) ? outputTokens.GetInt64() : null
            };
        }

        return chatResponse;
    }
}
