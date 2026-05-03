namespace SharedKernel.Emails;

internal sealed class FileSystemEmailTemplateLoader(string emailsDistPath, bool reloadOnEachRender) : IEmailTemplateLoader
{
    private readonly Dictionary<string, string> _cache = new();
    private readonly Lock _cacheLock = new();

    public string LoadHtml(string name, string locale)
    {
        return Read(name, locale, "html");
    }

    public string LoadPlainText(string name, string locale)
    {
        return Read(name, locale, "txt");
    }

    private string Read(string name, string locale, string extension)
    {
        var fileName = $"{name}.{locale}.{extension}";
        var path = Path.Combine(emailsDistPath, fileName);

        if (reloadOnEachRender)
        {
            return ReadFromDisk(path, fileName);
        }

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(path, out var cached)) return cached;
            var contents = ReadFromDisk(path, fileName);
            _cache[path] = contents;
            return contents;
        }
    }

    private static string ReadFromDisk(string path, string fileName)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Email template '{fileName}' not found.", path);
        }

        return File.ReadAllText(path);
    }
}
