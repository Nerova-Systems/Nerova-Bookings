using System.Text.Json;
using BackOffice.Database;
using BackOffice.Features.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Catalog;
using SharedKernel.Configuration;
using SharedKernel.Cqrs;

namespace BackOffice.Features.Catalog.Commands;

public sealed record IngestCatalogEventCommand(Guid SourceEventId, string Type, string Payload, DateTimeOffset OccurredAt)
    : ICommand, IRequest<Result>;

public sealed class IngestCatalogEventHandler(BackOfficeDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<IngestCatalogEventCommand, Result>
{
    public async Task<Result> Handle(IngestCatalogEventCommand command, CancellationToken cancellationToken)
    {
        if (await dbContext.Set<ProcessedCatalogEvent>().AnyAsync(e => e.Id == command.SourceEventId, cancellationToken))
        {
            return Result.Success();
        }

        var result = command.Type switch
        {
            var type when type == typeof(TenantCatalogUpserted).FullName => await IngestTenantUpserted(command, cancellationToken),
            var type when type == typeof(TenantCatalogDeleted).FullName => await IngestTenantDeleted(command, cancellationToken),
            var type when type == typeof(UserCatalogUpserted).FullName => await IngestUserUpserted(command, cancellationToken),
            var type when type == typeof(UserCatalogDeleted).FullName => await IngestUserDeleted(command, cancellationToken),
            _ => Result.BadRequest($"Catalog event type '{command.Type}' is not supported.")
        };

        if (!result.IsSuccess) return result;

        await dbContext.Set<ProcessedCatalogEvent>().AddAsync(ProcessedCatalogEvent.Create(command.SourceEventId, timeProvider.GetUtcNow()), cancellationToken);

        return Result.Success();
    }

    private async Task<Result> IngestTenantUpserted(IngestCatalogEventCommand command, CancellationToken cancellationToken)
    {
        var catalogEvent = Deserialize<TenantCatalogUpserted>(command.Payload);
        var tenant = await dbContext.Set<CatalogTenant>().SingleOrDefaultAsync(t => t.Id == catalogEvent.TenantId, cancellationToken);
        var isNew = tenant is null;
        tenant ??= CatalogTenant.Create(catalogEvent.TenantId);

        tenant.Upsert(catalogEvent.Name, catalogEvent.State, catalogEvent.Plan, catalogEvent.LogoUrl, catalogEvent.CreatedAt, catalogEvent.ModifiedAt, command.OccurredAt);
        if (isNew) await dbContext.AddAsync(tenant, cancellationToken);
        else dbContext.Update(tenant);

        return Result.Success();
    }

    private async Task<Result> IngestTenantDeleted(IngestCatalogEventCommand command, CancellationToken cancellationToken)
    {
        var catalogEvent = Deserialize<TenantCatalogDeleted>(command.Payload);
        var tenant = await dbContext.Set<CatalogTenant>().SingleOrDefaultAsync(t => t.Id == catalogEvent.TenantId, cancellationToken);
        var isNew = tenant is null;
        tenant ??= CatalogTenant.Create(catalogEvent.TenantId);

        tenant.MarkDeleted(catalogEvent.DeletedAt, command.OccurredAt);
        if (isNew) await dbContext.AddAsync(tenant, cancellationToken);
        else dbContext.Update(tenant);

        return Result.Success();
    }

    private async Task<Result> IngestUserUpserted(IngestCatalogEventCommand command, CancellationToken cancellationToken)
    {
        var catalogEvent = Deserialize<UserCatalogUpserted>(command.Payload);
        var user = await dbContext.Set<CatalogUser>().SingleOrDefaultAsync(u => u.Id == catalogEvent.UserId, cancellationToken);
        var isNew = user is null;
        user ??= CatalogUser.Create(catalogEvent.UserId);

        user.Upsert(catalogEvent.TenantId, catalogEvent.Email, catalogEvent.Role, catalogEvent.FirstName, catalogEvent.LastName, catalogEvent.Title, catalogEvent.EmailConfirmed, catalogEvent.CreatedAt, catalogEvent.ModifiedAt, catalogEvent.LastSeenAt, command.OccurredAt);
        if (isNew) await dbContext.AddAsync(user, cancellationToken);
        else dbContext.Update(user);

        return Result.Success();
    }

    private async Task<Result> IngestUserDeleted(IngestCatalogEventCommand command, CancellationToken cancellationToken)
    {
        var catalogEvent = Deserialize<UserCatalogDeleted>(command.Payload);
        var user = await dbContext.Set<CatalogUser>().SingleOrDefaultAsync(u => u.Id == catalogEvent.UserId, cancellationToken);
        var isNew = user is null;
        user ??= CatalogUser.Create(catalogEvent.UserId);

        user.MarkDeleted(catalogEvent.TenantId, catalogEvent.DeletedAt, command.OccurredAt);
        if (isNew) await dbContext.AddAsync(user, cancellationToken);
        else dbContext.Update(user);

        return Result.Success();
    }

    private static T Deserialize<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, SharedDependencyConfiguration.DefaultJsonSerializerOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize catalog event '{typeof(T).Name}'.");
    }
}
