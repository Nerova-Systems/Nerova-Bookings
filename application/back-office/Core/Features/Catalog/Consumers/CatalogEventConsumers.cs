using System.Text.Json;
using BackOffice.Features.Catalog.Commands;
using MassTransit;
using SharedKernel.Catalog;
using SharedKernel.Configuration;

namespace BackOffice.Features.Catalog.Consumers;

public abstract class CatalogEventConsumer<TEvent>(IMediator mediator, TimeProvider timeProvider) : IConsumer<TEvent>
    where TEvent : class
{
    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var sourceEventId = context.MessageId ?? NewId.NextGuid();
        var occurredAt = context.SentTime is { } sentTime
            ? new DateTimeOffset(DateTime.SpecifyKind(sentTime, DateTimeKind.Utc))
            : timeProvider.GetUtcNow();
        var payload = JsonSerializer.Serialize(context.Message, SharedDependencyConfiguration.DefaultJsonSerializerOptions);

        await mediator.Send(
            new IngestCatalogEventCommand(sourceEventId, typeof(TEvent).FullName!, payload, occurredAt),
            context.CancellationToken
        );
    }
}

public sealed class TenantCatalogUpsertedConsumer(IMediator mediator, TimeProvider timeProvider)
    : CatalogEventConsumer<TenantCatalogUpserted>(mediator, timeProvider);

public sealed class TenantCatalogDeletedConsumer(IMediator mediator, TimeProvider timeProvider)
    : CatalogEventConsumer<TenantCatalogDeleted>(mediator, timeProvider);

public sealed class UserCatalogUpsertedConsumer(IMediator mediator, TimeProvider timeProvider)
    : CatalogEventConsumer<UserCatalogUpserted>(mediator, timeProvider);

public sealed class UserCatalogDeletedConsumer(IMediator mediator, TimeProvider timeProvider)
    : CatalogEventConsumer<UserCatalogDeleted>(mediator, timeProvider);
