using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Apps.Domain;

public interface ICredentialRepository : ICrudRepository<Credential, CredentialId>
{
    Task<Credential?> GetForUserAsync(UserId userId, AppSlug appSlug, CancellationToken cancellationToken);

    Task<Credential[]> GetForUserAsync(UserId userId, CancellationToken cancellationToken);
}

public sealed class CredentialRepository(MainDbContext mainDbContext)
    : RepositoryBase<Credential, CredentialId>(mainDbContext), ICredentialRepository
{
    public async Task<Credential?> GetForUserAsync(UserId userId, AppSlug appSlug, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(credential => credential.UserId == userId && credential.AppSlug == appSlug)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Credential[]> GetForUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(credential => credential.UserId == userId)
            .ToArrayAsync(cancellationToken);
    }
}
