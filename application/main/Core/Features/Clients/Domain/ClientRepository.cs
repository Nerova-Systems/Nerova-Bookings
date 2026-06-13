using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Clients.Domain;

public interface IClientRepository : ICrudRepository<Client, ClientId>, IBulkRemoveRepository<Client>
{
    Task<Client[]> GetByIdsAsync(ClientId[] ids, CancellationToken cancellationToken);

    Task<(Client[] Clients, int TotalItems, int TotalPages)> Search(
        string? search,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        SortableClientProperties? orderBy,
        SortOrder? sortOrder,
        int? pageOffset,
        int? pageSize,
        CancellationToken cancellationToken
    );

    /// <summary>
    ///     Finds an existing client for the tenant matching the given phone number (preferred) or email,
    ///     bypassing the tenant query filter. Used by the booking-created upsert, which runs for anonymous
    ///     public bookings where no tenant context is set.
    /// </summary>
    Task<Client?> GetByTenantAndContactUnfilteredAsync(TenantId tenantId, string? phoneNumber, string? email, CancellationToken cancellationToken);

    /// <summary>
    ///     All clients of a tenant for in-memory duplicate detection during imports (avoids per-row
    ///     lookups). Unfiltered so the import pipeline can run from background contexts too.
    /// </summary>
    Task<Client[]> GetAllForDuplicateCheckUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Fetches a client by id with an explicit tenant guard, bypassing the tenant query filter.
    ///     Used by anonymous receptionist webhook paths where no tenant context is set.
    /// </summary>
    Task<Client?> GetByIdUnfilteredAsync(TenantId tenantId, ClientId id, CancellationToken cancellationToken);
}

public sealed class ClientRepository(MainDbContext mainDbContext)
    : RepositoryBase<Client, ClientId>(mainDbContext), IClientRepository
{
    public async Task<Client[]> GetByIdsAsync(ClientId[] ids, CancellationToken cancellationToken)
    {
        return await DbSet.Where(client => ids.AsEnumerable().Contains(client.Id)).ToArrayAsync(cancellationToken);
    }

    public async Task<(Client[] Clients, int TotalItems, int TotalPages)> Search(
        string? search,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        SortableClientProperties? orderBy,
        SortOrder? sortOrder,
        int? pageOffset,
        int? pageSize,
        CancellationToken cancellationToken
    )
    {
        IQueryable<Client> query = DbSet;

        if (search is not null)
        {
            // Concatenate first and last name to enable searching by full name.
            query = query.Where(client =>
                (client.FirstName + " " + client.LastName).Contains(search) ||
                (client.Email ?? "").Contains(search) ||
                (client.PhoneNumber ?? "").Contains(search)
            );
        }

        // Materialize before date filtering/sorting: SQLite (used in tests) cannot translate
        // DateTimeOffset comparisons or ORDER BY. Volume is bounded by the tenant query filter.
        var all = await query.ToArrayAsync(cancellationToken);

        IEnumerable<Client> filtered = all;

        if (startDate is not null)
        {
            filtered = filtered.Where(client => client.CreatedAt >= startDate.Value);
        }

        if (endDate is not null)
        {
            filtered = filtered.Where(client => client.CreatedAt < endDate.Value.AddDays(1));
        }

        var ascending = sortOrder != SortOrder.Descending;

        filtered = orderBy switch
        {
            SortableClientProperties.FirstVisitAt => ascending
                ? filtered.OrderBy(client => client.CreatedAt)
                : filtered.OrderByDescending(client => client.CreatedAt),
            SortableClientProperties.LastVisitAt => ascending
                ? filtered.OrderBy(client => client.LastVisitAt is null ? 1 : 0).ThenBy(client => client.LastVisitAt)
                : filtered.OrderBy(client => client.LastVisitAt is null ? 1 : 0).ThenByDescending(client => client.LastVisitAt),
            SortableClientProperties.Email => ascending
                ? filtered.OrderBy(client => client.Email)
                : filtered.OrderByDescending(client => client.Email),
            _ => ascending
                ? filtered.OrderBy(client => client.FirstName).ThenBy(client => client.LastName).ThenBy(client => client.Email)
                : filtered.OrderByDescending(client => client.FirstName).ThenByDescending(client => client.LastName).ThenByDescending(client => client.Email)
        };

        var ordered = filtered.ToArray();

        var size = pageSize ?? 25;
        var totalItems = ordered.Length;
        var totalPages = totalItems == 0 || size == 0 ? 0 : (totalItems - 1) / size + 1;
        var result = size == 0 ? [] : ordered.Skip((pageOffset ?? 0) * size).Take(size).ToArray();

        return (result, totalItems, totalPages);
    }

    public async Task<Client?> GetByTenantAndContactUnfilteredAsync(TenantId tenantId, string? phoneNumber, string? email, CancellationToken cancellationToken)
    {
        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        if (normalizedPhone is not null)
        {
            var byPhone = await DbSet
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .FirstOrDefaultAsync(client => client.TenantId == tenantId && client.PhoneNumber == normalizedPhone, cancellationToken);
            if (byPhone is not null) return byPhone;
        }

        if (normalizedEmail is not null)
        {
            return await DbSet
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .FirstOrDefaultAsync(client => client.TenantId == tenantId && client.Email == normalizedEmail, cancellationToken);
        }

        return null;
    }

    public async Task<Client[]> GetAllForDuplicateCheckUnfilteredAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(client => client.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Client?> GetByIdUnfilteredAsync(TenantId tenantId, ClientId id, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .FirstOrDefaultAsync(client => client.TenantId == tenantId && client.Id == id, cancellationToken);
    }
}
