using Account.Features.Catalog;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Outbox;
using SharedKernel.Telemetry;

namespace Account.Features.Tenants.Commands;

[PublicAPI]
public sealed record RestoreTenantCommand(TenantId Id) : ICommand, IRequest<Result>;

public sealed class RestoreTenantHandler(ITenantRepository tenantRepository, IOutboxPublisher outboxPublisher, ITelemetryEventsCollector events)
    : IRequestHandler<RestoreTenantCommand, Result>
{
    public async Task<Result> Handle(RestoreTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetDeletedByIdAsync(command.Id, cancellationToken);
        if (tenant is null) return Result.NotFound($"Deleted tenant with id '{command.Id}' not found.");

        tenantRepository.Restore(tenant);
        await outboxPublisher.EnqueueAsync(CatalogEventFactory.TenantUpserted(tenant), cancellationToken);

        events.CollectEvent(new TenantRestored(tenant.Id));

        return Result.Success();
    }
}
