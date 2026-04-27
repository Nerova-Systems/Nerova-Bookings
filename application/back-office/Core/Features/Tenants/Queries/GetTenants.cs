using BackOffice.Database;
using BackOffice.Features.Catalog.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace BackOffice.Features.Tenants.Queries;

[PublicAPI]
public sealed record GetTenantsQuery(string? Search = null, int? PageOffset = null, int PageSize = 25)
    : IRequest<Result<TenantsResponse>>;

[PublicAPI]
public sealed record TenantsResponse(int TotalCount, int PageSize, int TotalPages, int CurrentPageOffset, TenantSummary[] Tenants);

[PublicAPI]
public sealed record TenantSummary(TenantId Id, string Name, string State, string Plan, string? LogoUrl, DateTimeOffset CreatedAt, DateTimeOffset? ModifiedAt, DateTimeOffset? DeletedAt, int UserCount);

public sealed class GetTenantsHandler(BackOfficeDbContext dbContext)
    : IRequestHandler<GetTenantsQuery, Result<TenantsResponse>>
{
    public async Task<Result<TenantsResponse>> Handle(GetTenantsQuery query, CancellationToken cancellationToken)
    {
        IQueryable<CatalogTenant> tenants = dbContext.Set<CatalogTenant>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            tenants = tenants.Where(t => t.Name.Contains(query.Search));
        }

        var pageSize = query.PageSize;
        var pageOffset = query.PageOffset ?? 0;
        var totalCount = await tenants.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (totalCount - 1) / pageSize + 1;
        var pagedTenants = await tenants.OrderBy(t => t.Name).Skip(pageOffset * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        var tenantIds = pagedTenants.Select(t => t.Id).ToArray();
        var userCounts = await dbContext.Set<CatalogUser>()
            .Where(u => u.DeletedAt == null && tenantIds.AsEnumerable().Contains(u.TenantId))
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var response = pagedTenants
            .Select(t => new TenantSummary(t.Id, t.Name, t.State, t.Plan, t.LogoUrl, t.CreatedAt, t.ModifiedAt, t.DeletedAt, userCounts.GetValueOrDefault(t.Id)))
            .ToArray();

        return new TenantsResponse(totalCount, pageSize, totalPages, pageOffset, response);
    }
}
