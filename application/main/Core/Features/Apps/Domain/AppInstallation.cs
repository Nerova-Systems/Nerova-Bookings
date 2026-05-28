using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Apps.Domain;

[IdPrefix("ins")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AppInstallationId>))]
public sealed record AppInstallationId(string Value) : StronglyTypedUlid<AppInstallationId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A tenant-level marker that <see cref="App" /> identified by <see cref="AppSlug" /> has been
///     installed (i.e., is available) within the tenant. Per-user OAuth tokens are stored
///     separately as <see cref="Credential" /> rows. Admins use installations to see "Google
///     Calendar is enabled for this team" without needing to enumerate individual credentials.
/// </summary>
public sealed class AppInstallation : AggregateRoot<AppInstallationId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private AppInstallation() : base(AppInstallationId.NewId())
    {
        AppSlug = new AppSlug(string.Empty);
        InstalledByUserId = new UserId(string.Empty);
    }

    private AppInstallation(TenantId tenantId, AppSlug appSlug, UserId installedByUserId, DateTimeOffset installedAt)
        : base(AppInstallationId.NewId())
    {
        TenantId = tenantId;
        AppSlug = appSlug;
        InstalledByUserId = installedByUserId;
        InstalledAt = installedAt;
    }

    public AppSlug AppSlug { get; private set; }

    public UserId InstalledByUserId { get; private set; }

    public DateTimeOffset InstalledAt { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static AppInstallation Create(TenantId tenantId, AppSlug appSlug, UserId installedByUserId, DateTimeOffset installedAt)
    {
        return new AppInstallation(tenantId, appSlug, installedByUserId, installedAt);
    }
}
