using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Account.Features.Users.Domain;

public interface IUserPreferencesRepository : ICrudRepository<UserPreferences, UserPreferencesId>
{
    /// <summary>
    ///     Returns the preferences row for the given user, or <see langword="null" /> if none exists.
    ///     Callers that want default-on-miss semantics should fall back to
    ///     <see cref="UserPreferences.CreateDefault" />.
    /// </summary>
    Task<UserPreferences?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken);
}

public sealed class UserPreferencesRepository(AccountDbContext accountDbContext)
    : RepositoryBase<UserPreferences, UserPreferencesId>(accountDbContext), IUserPreferencesRepository
{
    public Task<UserPreferences?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }
}
