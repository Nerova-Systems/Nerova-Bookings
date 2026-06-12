using System.Text;
using System.Text.Json;
using Main.Features.DataImport.Domain;
using Microsoft.Extensions.AI;

namespace Main.Features.DataImport.Agent;

/// <summary>
///     Infers which uploaded CSV column maps to which client field (spec R18). The model proposes a
///     mapping as structured JSON from the headers plus a few sample rows; the deterministic heuristic
///     is both the fallback (no AI configured, model output unparseable) and the baseline the agent
///     must beat — so imports keep working when AI is down (spec ground rule 2).
/// </summary>
public sealed class ColumnMappingInferrer(IChatClient chatClient, ILogger<ColumnMappingInferrer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<ImportColumnMapping> InferAsync(string[] headers, string[][] sampleRows, CancellationToken cancellationToken)
    {
        var heuristicMapping = InferHeuristically(headers);

        try
        {
            var agentMapping = await InferWithModelAsync(headers, sampleRows, cancellationToken);
            if (agentMapping is not null && IsValidAgainstHeaders(agentMapping, headers))
            {
                return agentMapping;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Column mapping inference via model failed; using heuristic mapping");
        }

        return heuristicMapping;
    }

    private async Task<ImportColumnMapping?> InferWithModelAsync(string[] headers, string[][] sampleRows, CancellationToken cancellationToken)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Map the columns of a client-list CSV export to client fields. Respond with ONLY a JSON object, no prose, matching:");
        prompt.AppendLine("""{"first_name_column": string|null, "last_name_column": string|null, "full_name_column": string|null, "email_column": string|null, "phone_column": string|null, "notes_column": string|null, "confidence": number}""");
        prompt.AppendLine("Use full_name_column only when one column holds the entire name. Column values must be exact header names. confidence is 0..1.");
        prompt.AppendLine($"Headers: {string.Join(", ", headers)}");
        foreach (var row in sampleRows.Take(3))
        {
            prompt.AppendLine($"Sample: {string.Join(" | ", row)}");
        }

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt.ToString())], new ChatOptions { MaxOutputTokens = 400 }, cancellationToken);

        var text = response.Text.Trim();
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart) return null;

        var parsed = JsonSerializer.Deserialize<ModelColumnMapping>(text[jsonStart..(jsonEnd + 1)], JsonOptions);
        if (parsed is null) return null;

        return new ImportColumnMapping(
            parsed.FirstNameColumn,
            parsed.LastNameColumn,
            parsed.FullNameColumn,
            parsed.EmailColumn,
            parsed.PhoneColumn,
            parsed.NotesColumn,
            Math.Clamp(parsed.Confidence, 0, 1),
            "agent"
        );
    }

    public static ImportColumnMapping InferHeuristically(string[] headers)
    {
        string? firstNameColumn = null;
        string? lastNameColumn = null;
        string? fullNameColumn = null;
        string? emailColumn = null;
        string? phoneColumn = null;
        string? notesColumn = null;
        var matchedColumns = 0;

        foreach (var header in headers)
        {
            var normalized = header.Trim().ToLowerInvariant();

            if (emailColumn is null && (normalized.Contains("email") || normalized.Contains("e-mail") || normalized == "mail"))
            {
                emailColumn = header;
                matchedColumns++;
            }
            else if (phoneColumn is null && (normalized.Contains("phone") || normalized.Contains("cell") || normalized.Contains("mobile") || normalized.Contains("whatsapp") || normalized.Contains("tel") || normalized.Contains("contact number") || normalized == "number"))
            {
                phoneColumn = header;
                matchedColumns++;
            }
            else if (lastNameColumn is null && (normalized.Contains("last name") || normalized.Contains("lastname") || normalized.Contains("surname") || normalized.Contains("family name") || normalized == "last"))
            {
                lastNameColumn = header;
                matchedColumns++;
            }
            else if (firstNameColumn is null && (normalized.Contains("first name") || normalized.Contains("firstname") || normalized.Contains("given name") || normalized == "first"))
            {
                firstNameColumn = header;
                matchedColumns++;
            }
            else if (notesColumn is null && (normalized.Contains("note") || normalized.Contains("comment") || normalized.Contains("remark")))
            {
                notesColumn = header;
                matchedColumns++;
            }
            else if (fullNameColumn is null && (normalized is "name" or "full name" or "client" or "client name" or "customer" or "customer name"))
            {
                fullNameColumn = header;
                matchedColumns++;
            }
        }

        if (firstNameColumn is not null && fullNameColumn is not null)
        {
            // Prefer explicit first/last columns; the generic name column likely duplicates them.
            fullNameColumn = null;
        }

        var hasName = firstNameColumn is not null || fullNameColumn is not null;
        var confidence = hasName && (phoneColumn is not null || emailColumn is not null)
            ? Math.Min(0.9, 0.5 + matchedColumns * 0.1)
            : 0.3;

        return new ImportColumnMapping(firstNameColumn, lastNameColumn, fullNameColumn, emailColumn, phoneColumn, notesColumn, confidence, "heuristic");
    }

    private static bool IsValidAgainstHeaders(ImportColumnMapping mapping, string[] headers)
    {
        var headerSet = headers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referenced = new[] { mapping.FirstNameColumn, mapping.LastNameColumn, mapping.FullNameColumn, mapping.EmailColumn, mapping.PhoneColumn, mapping.NotesColumn };
        var hasName = mapping.FirstNameColumn is not null || mapping.FullNameColumn is not null;
        return hasName && referenced.All(column => column is null || headerSet.Contains(column));
    }

    private sealed record ModelColumnMapping(
        string? FirstNameColumn,
        string? LastNameColumn,
        string? FullNameColumn,
        string? EmailColumn,
        string? PhoneColumn,
        string? NotesColumn,
        double Confidence
    );
}
