using BackOffice.Database;
using BackOffice.Features.Catalog.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace BackOffice.Features.Tenants.Queries;

[PublicAPI]
public sealed record GetTenantByIdQuery(TenantId Id) : IRequest<Result<TenantDetails>>;

[PublicAPI]
public sealed record TenantDetails(TenantId Id, string Name, string State, string Plan, string? LogoUrl, DateTimeOffset CreatedAt, DateTimeOffset? ModifiedAt, DateTimeOffset? DeletedAt, UserSummary[] Users);

[PublicAPI]
public sealed record UserSummary(UserId Id, string Email, string Role, string FirstName, string LastName, string Title, bool EmailConfirmed, DateTimeOffset? LastSeenAt, DateTimeOffset? DeletedAt);

public sealed class GetTenantByIdHandler(BackOfficeDbContext dbContext)
    : IRequestHandler<GetTenantByIdQuery, Result<TenantDetails>>
{
    public async Task<Result<TenantDetails>> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<CatalogTenant>().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
        if (tenant is null) return Result<TenantDetails>.NotFound($"Tenant with id '{query.Id}' not found.");

        var users = await dbContext.Set<CatalogUser>()
            .Where(u => u.TenantId == query.Id)
            .OrderBy(u => u.Email)
            .Select(u => new UserSummary(u.Id, u.Email, u.Role, u.FirstName, u.LastName, u.Title, u.EmailConfirmed, u.LastSeenAt, u.DeletedAt))
            .ToArrayAsync(cancellationToken);

        return new TenantDetails(tenant.Id, tenant.Name, tenant.State, tenant.Plan, tenant.LogoUrl, tenant.CreatedAt, tenant.ModifiedAt, tenant.DeletedAt, users);
    }
}
