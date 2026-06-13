namespace DeveloperCli.Utilities;

public static class IncludeArgumentChunker
{
    // Windows CreateProcess caps the command line at 32 767 characters. A large branch diff
    // (hundreds of changed .cs files) overflows a single --include="a;b;c" argument, so the
    // file list is split into chunks and the JetBrains tool is invoked once per chunk.
    // 24 000 leaves generous headroom for the rest of the command line.
    private const int MaxIncludeCharacters = 24_000;

    public static IReadOnlyList<string[]> Chunk(string[] files)
    {
        var chunks = new List<string[]>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var file in files)
        {
            if (current.Count > 0 && currentLength + file.Length + 1 > MaxIncludeCharacters)
            {
                chunks.Add(current.ToArray());
                current = [];
                currentLength = 0;
            }

            current.Add(file);
            currentLength += file.Length + 1; // +1 for the joining semicolon
        }

        if (current.Count > 0) chunks.Add(current.ToArray());

        return chunks;
    }
}
