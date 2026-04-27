using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using SharedKernel.Catalog;

namespace Account.Features.Catalog;

internal static class CatalogEventFactory
{
    public static TenantCatalogUpserted TenantUpserted(Tenant tenant)
    {
        return new TenantCatalogUpserted(
            tenant.Id,
            tenant.Name,
            tenant.State.ToString(),
            tenant.Plan.ToString(),
            tenant.Logo.Url,
            tenant.CreatedAt,
            tenant.ModifiedAt
        );
    }

    public static UserCatalogUpserted UserUpserted(User user)
    {
        return new UserCatalogUpserted(
            user.Id,
            user.TenantId,
            user.Email,
            user.Role.ToString(),
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            user.Title ?? string.Empty,
            user.EmailConfirmed,
            user.CreatedAt,
            user.ModifiedAt,
            user.LastSeenAt
        );
    }
}
