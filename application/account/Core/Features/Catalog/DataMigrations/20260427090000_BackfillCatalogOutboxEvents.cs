using Account.Database;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Catalog;
using SharedKernel.Database;

namespace Account.Features.Catalog.DataMigrations;

public sealed class BackfillCatalogOutboxEvents(AccountDbContext dbContext, IPublishEndpoint publishEndpoint) : IDataMigration
{
    public string Id => "20260427090000_BackfillCatalogOutboxEvents";

    public TimeSpan Timeout => TimeSpan.FromMinutes(5);

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Set<Tenant>().IgnoreQueryFilters().ToArrayAsync(cancellationToken);
        var users = await dbContext.Set<User>().IgnoreQueryFilters().ToArrayAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            if (tenant.DeletedAt is null)
            {
                await publishEndpoint.Publish(CatalogEventFactory.TenantUpserted(tenant), cancellationToken);
            }
            else
            {
                await publishEndpoint.Publish(new TenantCatalogDeleted(tenant.Id, tenant.DeletedAt.Value), cancellationToken);
            }
        }

        foreach (var user in users)
        {
            if (user.DeletedAt is null)
            {
                await publishEndpoint.Publish(CatalogEventFactory.UserUpserted(user), cancellationToken);
            }
            else
            {
                await publishEndpoint.Publish(new UserCatalogDeleted(user.Id, user.TenantId, user.DeletedAt.Value), cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return $"Backfilled {tenants.Length} tenants and {users.Length} users to the catalog outbox.";
    }
}
