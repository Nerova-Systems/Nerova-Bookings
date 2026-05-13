using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Tenants.BackOffice.Commands;

[PublicAPI]
public sealed record ReconcileTenantWithStripeCommand : ICommand, IRequest<Result<ReconcileTenantWithStripeResponse>>
{
    [JsonIgnore]
    public TenantId TenantId { get; init; } = null!;
}

[PublicAPI]
public sealed record ReconcileTenantWithStripeResponse(
    int BillingEventsAppended,
    bool HasDriftDetected,
    int DriftDiscrepancyCount,
    DateTimeOffset ReconciledAt,
    ArchivedEventsAwaitingConfirmation? ArchivedEventsAwaitingConfirmation
);

[PublicAPI]
public sealed record ArchivedEventsAwaitingConfirmation(int Count, DateTimeOffset OldestOccurredAt, DateTimeOffset NewestOccurredAt);

public sealed class ReconcileTenantWithStripeHandler(
    ITenantRepository tenantRepository,
    ISubscriptionRepository subscriptionRepository,
    TimeProvider timeProvider
) : IRequestHandler<ReconcileTenantWithStripeCommand, Result<ReconcileTenantWithStripeResponse>>
{
    public async Task<Result<ReconcileTenantWithStripeResponse>> Handle(ReconcileTenantWithStripeCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (tenant is null) return Result<ReconcileTenantWithStripeResponse>.NotFound($"Tenant with id '{command.TenantId}' not found.");

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (subscription is null) return Result<ReconcileTenantWithStripeResponse>.NotFound($"Subscription for tenant '{command.TenantId}' not found.");

        return new ReconcileTenantWithStripeResponse(
            0,
            subscription.HasDriftDetected,
            subscription.DriftDiscrepancies.Length,
            timeProvider.GetUtcNow(),
            null
        );
    }
}
