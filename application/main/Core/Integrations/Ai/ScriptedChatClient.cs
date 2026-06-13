using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Main.Integrations.Ai;

/// <summary>
///     Deterministic <see cref="IChatClient" /> used when no AI API key is configured (tests, local dev).
///     It never calls a network. Behavior is scripted through directives embedded in the user message —
///     the mechanism the API tests use to exercise the full agent pipeline (tool invocation, thread
///     persistence, budgets, escalation) without a live model (docs/agentic-system-spec.md §6.7):
///     <code>
///         @tool GetEventTypes {}
///         @tool CreateBooking {"serviceSlug":"gel-set","startTime":"2026-06-13T09:00:00Z"}
///         @reply Thanks! You are booked.
///     </code>
///     Each <c>@tool</c> directive produces one function call (in order, one per model round-trip); once
///     all have results, the <c>@reply</c> text (or a default acknowledgement) is returned. Messages
///     without directives get the default acknowledgement, so an unconfigured local environment still
///     answers WhatsApp messages deterministically.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    public const string DefaultReply = "Thanks for your message! A team member will be with you shortly.";

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null, CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var lastUserTextIndex = messageList.FindLastIndex(m => m.Role == ChatRole.User && m.Contents.OfType<TextContent>().Any(t => t.Text.Length > 0));
        var lastUserText = lastUserTextIndex >= 0 ? messageList[lastUserTextIndex].Text : string.Empty;

        var directives = ParseDirectives(lastUserText);

        // Only tool results produced after the current user message count toward this turn's script —
        // earlier turns in the resumed thread carry their own completed tool calls.
        var completedToolCalls = messageList.Skip(lastUserTextIndex + 1).SelectMany(m => m.Contents).OfType<FunctionResultContent>().Count();

        ChatMessage responseMessage;
        ChatFinishReason finishReason;
        if (completedToolCalls < directives.ToolCalls.Count)
        {
            var (name, argumentsJson) = directives.ToolCalls[completedToolCalls];
            var arguments = ParseArguments(argumentsJson);
            responseMessage = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"scripted_call_{completedToolCalls + 1}", name, arguments)]);
            finishReason = ChatFinishReason.ToolCalls;
        }
        else
        {
            responseMessage = new ChatMessage(ChatRole.Assistant, directives.Reply ?? DefaultReply);
            finishReason = ChatFinishReason.Stop;
        }

        var response = new ChatResponse(responseMessage)
        {
            ModelId = "scripted",
            FinishReason = finishReason,
            Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 }
        };
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    }

    private static ScriptDirectives ParseDirectives(string text)
    {
        var toolCalls = new List<(string Name, string ArgumentsJson)>();
        string? reply = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("@tool ", StringComparison.Ordinal))
            {
                var remainder = line["@tool ".Length..].Trim();
                var spaceIndex = remainder.IndexOf(' ');
                var name = spaceIndex < 0 ? remainder : remainder[..spaceIndex];
                var argumentsJson = spaceIndex < 0 ? "{}" : remainder[(spaceIndex + 1)..].Trim();
                toolCalls.Add((name, argumentsJson));
            }
            else if (line.StartsWith("@reply ", StringComparison.Ordinal))
            {
                reply = line["@reply ".Length..].Trim();
            }
        }

        return new ScriptDirectives(toolCalls, reply);
    }

    private static Dictionary<string, object?>? ParseArguments(string argumentsJson)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            return parsed?.ToDictionary(pair => pair.Key, object? (pair) => pair.Value);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }

    private sealed record ScriptDirectives(List<(string Name, string ArgumentsJson)> ToolCalls, string? Reply);
}
