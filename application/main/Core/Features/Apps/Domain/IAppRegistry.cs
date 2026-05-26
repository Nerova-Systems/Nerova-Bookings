namespace Main.Features.Apps.Domain;

/// <summary>
///     Resolves a per-slug <see cref="IAppInstaller" /> registered by connector tracks. Returns
///     <see langword="null" /> when no installer is registered for the given slug, so the
///     foundation track can ship without any connector implementations and still expose the
///     install / callback / uninstall endpoints.
/// </summary>
public interface IAppRegistry
{
    IEnumerable<AppSlug> RegisteredSlugs { get; }

    IAppInstaller? Resolve(AppSlug slug);
}

/// <summary>
///     Default registry that resolves installers from DI. Connector tracks register
///     <see cref="IAppInstaller" /> implementations as scoped/singleton services; this registry
///     selects the matching one by <see cref="IAppInstaller.Slug" />.
/// </summary>
public sealed class AppRegistry(IEnumerable<IAppInstaller> installers) : IAppRegistry
{
    private readonly IReadOnlyDictionary<AppSlug, IAppInstaller> _installers =
        installers.ToDictionary(installer => installer.Slug);

    public IAppInstaller? Resolve(AppSlug slug)
    {
        return _installers.GetValueOrDefault(slug);
    }

    public IEnumerable<AppSlug> RegisteredSlugs => _installers.Keys;
}
