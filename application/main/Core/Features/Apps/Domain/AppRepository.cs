using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Apps.Domain;

public interface IAppRepository : ICrudRepository<App, AppSlug>
{
    Task<App[]> GetAllAsync(CancellationToken cancellationToken);

    Task<App[]> GetActiveAsync(CancellationToken cancellationToken);
}

public sealed class AppRepository(MainDbContext mainDbContext)
    : RepositoryBase<App, AppSlug>(mainDbContext), IAppRepository
{
    public async Task<App[]> GetAllAsync(CancellationToken cancellationToken)
    {
        return await DbSet
            .OrderBy(app => app.Category)
            .ThenBy(app => app.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<App[]> GetActiveAsync(CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(app => app.IsActive)
            .OrderBy(app => app.Category)
            .ThenBy(app => app.Name)
            .ToArrayAsync(cancellationToken);
    }
}
