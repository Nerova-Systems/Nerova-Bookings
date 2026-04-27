using Account.Features.Catalog;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Catalog;
using SharedKernel.Domain;
using SharedKernel.Outbox;
using SharedKernel.Telemetry;

namespace Account.Features.Tenants.Commands;

[PublicAPI]
public sealed record DeleteTenantCommand(TenantId Id) : ICommand, IRequest<Result>;

public sealed class DeleteTenantHandler(ITenantRepository tenantRepository, ISubscriptionRepository subscriptionRepository, IOutboxPublisher outboxPublisher, ITelemetryEventsCollector events, TimeProvider timeProvider)
    : IRequestHandler<DeleteTenantCommand, Result>
{
    public async Task<Result> Handle(DeleteTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(command.Id, cancellationToken);
        if (tenant is null) return Result.NotFound($"Tenant with id '{command.Id}' not found.");

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.Id, cancellationToken);
        if (subscription?.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue)
        {
            return Result.BadRequest("Cannot delete a tenant with an active subscription.");
        }

        tenantRepository.Remove(tenant);
        await outboxPublisher.EnqueueAsync(new TenantCatalogDeleted(tenant.Id, timeProvider.GetUtcNow()), cancellationToken);

        events.CollectEvent(new TenantDeleted(tenant.Id, tenant.State));

        return Result.Success();
    }
}
