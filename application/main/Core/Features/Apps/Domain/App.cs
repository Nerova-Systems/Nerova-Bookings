using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Apps.Domain;

/// <summary>
///     Strongly-typed slug used as the primary identifier for <see cref="App" /> registry entries
///     and as the foreign key for <see cref="Credential" /> and <see cref="AppInstallation" />.
///     Slugs are stable, human-readable identifiers (e.g. <c>google-calendar</c>, <c>zoom</c>)
///     chosen by the connector author; mirrors cal.com's <c>App.slug</c> column.
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AppSlug>))]
public sealed record AppSlug(string Value) : StronglyTypedString<AppSlug>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Categories the App registry uses to group installable connectors. Mirrors the high-level
///     cal.com <c>AppCategories</c> vocabulary but is intentionally trimmed to the categories we
///     plan to support in Wave 5.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppCategory
{
    Calendar,
    Conferencing,
    Payment,
    Other
}

/// <summary>
///     Registry record describing an installable third-party application (connector). The set of
///     <c>App</c> rows is owned by the platform and seeded at startup by connector tracks
///     (T2-Google-Calendar, T2-Zoom, etc.). Tenants do not create or mutate <c>App</c> rows —
///     they create <see cref="AppInstallation" /> rows referencing a slug instead.
/// </summary>
public sealed class App : AggregateRoot<AppSlug>
{
    [UsedImplicitly]
    private App() : base(new AppSlug(string.Empty))
    {
        Name = string.Empty;
        Description = string.Empty;
        LogoUrl = string.Empty;
    }

    private App(AppSlug slug, string name, AppCategory category, string description, string logoUrl, bool isActive)
        : base(slug)
    {
        Name = name.Trim();
        Category = category;
        Description = description.Trim();
        LogoUrl = logoUrl.Trim();
        IsActive = isActive;
    }

    public string Name { get; private set; }

    public AppCategory Category { get; private set; }

    public string Description { get; private set; }

    public string LogoUrl { get; private set; }

    public bool IsActive { get; private set; }

    public static App Create(AppSlug slug, string name, AppCategory category, string description, string logoUrl, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(slug.Value)) throw new ArgumentException("App slug is required.", nameof(slug));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("App name is required.", nameof(name));

        return new App(slug, name, category, description ?? string.Empty, logoUrl ?? string.Empty, isActive);
    }

    public void Update(string name, AppCategory category, string description, string logoUrl, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("App name is required.", nameof(name));

        Name = name.Trim();
        Category = category;
        Description = (description ?? string.Empty).Trim();
        LogoUrl = (logoUrl ?? string.Empty).Trim();
        IsActive = isActive;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
