using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel.Domain;

namespace SharedKernel.EntityFramework;

/// <summary>
///     The UpdateAuditableEntitiesInterceptor is a SaveChangesInterceptor that updates the ModifiedAt property
///     for IAuditableEntity instances when changes are made to the database.
/// </summary>
public sealed class UpdateAuditableEntitiesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        UpdateEntities(eventData);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateEntities(DbContextEventData eventData)
    {
        var dbContext = eventData.Context ?? throw new UnreachableException("The 'eventData.Context' property is unexpectedly null.");

        var timeProvider = dbContext is ITimeProviderSource timeProviderSource
            ? timeProviderSource.TimeProvider
            : TimeProvider.System;

        var audibleEntities = dbContext.ChangeTracker.Entries<IAuditableEntity>();

        foreach (var entityEntry in audibleEntities)
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (entityEntry.State)
            {
                case EntityState.Added when entityEntry.Entity.CreatedAt == default:
                    throw new UnreachableException("CreatedAt must be set before saving.");
                case EntityState.Added:
                    // Initialize ModifiedAt to CreatedAt on first INSERT so the [ConcurrencyCheck]
                    // column is never NULL. A NULL value causes EF to generate "WHERE modified_at = NULL"
                    // which always evaluates to FALSE in PostgreSQL, making the first UPDATE fail.
                    entityEntry.Entity.UpdateModifiedAt(entityEntry.Entity.CreatedAt);
                    break;
                case EntityState.Modified:
                    entityEntry.Entity.UpdateModifiedAt(timeProvider.GetUtcNow());
                    break;
            }
        }
    }
}
