using System.Text;

namespace Main.Features.DataImport.Shared;

/// <summary>
///     Minimal RFC 4180 CSV parser: quoted fields, escaped quotes, embedded commas/newlines, CRLF or LF
///     line endings. The first record is the header row. Intentionally dependency-free.
/// </summary>
public static class CsvParser
{
    public static CsvDocument Parse(string content)
    {
        var records = ParseRecords(content);
        if (records.Count == 0)
        {
            return new CsvDocument([], []);
        }

        var headers = records[0].Select(header => header.Trim()).ToArray();
        var rows = records
            .Skip(1)
            .Where(record => record.Any(field => field.Trim().Length > 0))
            .Select(record => PadToLength(record, headers.Length))
            .ToArray();

        return new CsvDocument(headers, rows);
    }

    private static string[] PadToLength(string[] record, int length)
    {
        if (record.Length == length) return record;
        if (record.Length > length) return record[..length];

        var padded = new string[length];
        record.CopyTo(padded, 0);
        for (var index = record.Length; index < length; index++)
        {
            padded[index] = string.Empty;
        }

        return padded;
    }

    private static List<string[]> ParseRecords(string content)
    {
        var records = new List<string[]>();
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (insideQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        currentField.Append('"');
                        index++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    insideQuotes = true;
                    break;
                case ',':
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add([.. currentRecord]);
                    currentRecord.Clear();
                    break;
                default:
                    currentField.Append(character);
                    break;
            }
        }

        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            records.Add([.. currentRecord]);
        }

        return records;
    }

    public sealed record CsvDocument(string[] Headers, string[][] Rows);
}
