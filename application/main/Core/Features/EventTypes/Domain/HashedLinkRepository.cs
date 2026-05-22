using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.EventTypes.Domain;

public interface IHashedLinkRepository : ICrudRepository<HashedLink, HashedLinkId>
{
    Task<HashedLink[]> GetForEventTypeAsync(EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<HashedLink?> GetByHashAsync(string hash, CancellationToken cancellationToken);

    Task<bool> HashExistsAsync(string hash, HashedLinkId? excludedId, CancellationToken cancellationToken);
}

public sealed class HashedLinkRepository(MainDbContext mainDbContext)
    : RepositoryBase<HashedLink, HashedLinkId>(mainDbContext), IHashedLinkRepository
{
    public async Task<HashedLink[]> GetForEventTypeAsync(EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(link => link.EventTypeId == eventTypeId)
            .OrderBy(link => link.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HashedLink?> GetByHashAsync(string hash, CancellationToken cancellationToken)
    {
        var normalized = hash.Trim();
        return await DbSet.FirstOrDefaultAsync(link => link.Hash == normalized, cancellationToken);
    }

    public async Task<bool> HashExistsAsync(string hash, HashedLinkId? excludedId, CancellationToken cancellationToken)
    {
        var normalized = hash.Trim();
        var query = DbSet.Where(link => link.Hash == normalized);
        if (excludedId is not null)
        {
            query = query.Where(link => link.Id != excludedId);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
