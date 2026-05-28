using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Apps.Domain;

public interface IAppInstallationRepository : ICrudRepository<AppInstallation, AppInstallationId>
{
    Task<AppInstallation?> GetForTenantAsync(AppSlug appSlug, CancellationToken cancellationToken);

    Task<AppInstallation[]> GetForTenantAsync(CancellationToken cancellationToken);
}

public sealed class AppInstallationRepository(MainDbContext mainDbContext)
    : RepositoryBase<AppInstallation, AppInstallationId>(mainDbContext), IAppInstallationRepository
{
    public async Task<AppInstallation?> GetForTenantAsync(AppSlug appSlug, CancellationToken cancellationToken)
    {
        return await DbSet.FirstOrDefaultAsync(installation => installation.AppSlug == appSlug, cancellationToken);
    }

    public async Task<AppInstallation[]> GetForTenantAsync(CancellationToken cancellationToken)
    {
        return await DbSet.OrderBy(installation => installation.AppSlug).ToArrayAsync(cancellationToken);
    }
}
