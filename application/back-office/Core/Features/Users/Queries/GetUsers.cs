using BackOffice.Database;
using BackOffice.Features.Catalog.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace BackOffice.Features.Users.Queries;

[PublicAPI]
public sealed record GetUsersQuery(string? Search = null, int? PageOffset = null, int PageSize = 25)
    : IRequest<Result<UsersResponse>>;

[PublicAPI]
public sealed record UsersResponse(int TotalCount, int PageSize, int TotalPages, int CurrentPageOffset, UserDetails[] Users);

[PublicAPI]
public sealed record UserDetails(UserId Id, TenantId TenantId, string TenantName, string Email, string Role, string FirstName, string LastName, string Title, bool EmailConfirmed, DateTimeOffset? LastSeenAt, DateTimeOffset? DeletedAt);

public sealed class GetUsersHandler(BackOfficeDbContext dbContext)
    : IRequestHandler<GetUsersQuery, Result<UsersResponse>>
{
    public async Task<Result<UsersResponse>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        IQueryable<CatalogUser> users = dbContext.Set<CatalogUser>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            users = users.Where(u => u.Email.Contains(query.Search) || u.FirstName.Contains(query.Search) || u.LastName.Contains(query.Search));
        }

        var pageSize = query.PageSize;
        var pageOffset = query.PageOffset ?? 0;
        var totalCount = await users.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (totalCount - 1) / pageSize + 1;
        var pagedUsers = await users.OrderBy(u => u.Email).Skip(pageOffset * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        var tenantIds = pagedUsers.Select(u => u.TenantId).Distinct().ToArray();
        var tenants = await dbContext.Set<CatalogTenant>().Where(t => tenantIds.AsEnumerable().Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var response = pagedUsers
            .Select(u => new UserDetails(u.Id, u.TenantId, tenants.GetValueOrDefault(u.TenantId, string.Empty), u.Email, u.Role, u.FirstName, u.LastName, u.Title, u.EmailConfirmed, u.LastSeenAt, u.DeletedAt))
            .ToArray();

        return new UsersResponse(totalCount, pageSize, totalPages, pageOffset, response);
    }
}
