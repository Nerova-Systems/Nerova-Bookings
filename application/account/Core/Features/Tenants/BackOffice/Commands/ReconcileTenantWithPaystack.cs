using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Tenants.BackOffice.Commands;

[PublicAPI]
public sealed record ReconcileTenantWithPaystackCommand : ICommand, IRequest<Result<ReconcileTenantWithPaystackResponse>>
{
    [JsonIgnore]
    public TenantId TenantId { get; init; } = null!;
}

[PublicAPI]
public sealed record ReconcileTenantWithPaystackResponse(
    int BillingEventsAppended,
    int RecoveredPaymentAttempts,
    bool HasDriftDetected,
    int DriftDiscrepancyCount,
    DateTimeOffset ReconciledAt,
    ArchivedEventsAwaitingConfirmation? ArchivedEventsAwaitingConfirmation
);

[PublicAPI]
public sealed record ArchivedEventsAwaitingConfirmation(int Count, DateTimeOffset OldestOccurredAt, DateTimeOffset NewestOccurredAt);

public sealed class ReconcileTenantWithPaystackHandler(
    ITenantRepository tenantRepository,
    ISubscriptionRepository subscriptionRepository,
    ProcessPendingPaystackEvents processPendingPaystackEvents,
    TimeProvider timeProvider
) : IRequestHandler<ReconcileTenantWithPaystackCommand, Result<ReconcileTenantWithPaystackResponse>>
{
    public async Task<Result<ReconcileTenantWithPaystackResponse>> Handle(ReconcileTenantWithPaystackCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (tenant is null) return Result<ReconcileTenantWithPaystackResponse>.NotFound($"Tenant with id '{command.TenantId}' not found.");

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (subscription is null) return Result<ReconcileTenantWithPaystackResponse>.NotFound($"Subscription for tenant '{command.TenantId}' not found.");
        if (subscription.PaystackCustomerId is null) return Result<ReconcileTenantWithPaystackResponse>.BadRequest("Subscription does not have a Paystack customer.");

        var result = await processPendingPaystackEvents.ExecuteAsync(subscription.PaystackCustomerId, cancellationToken);
        var reloadedSubscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (reloadedSubscription is null) return Result<ReconcileTenantWithPaystackResponse>.NotFound($"Subscription for tenant '{command.TenantId}' not found.");

        return new ReconcileTenantWithPaystackResponse(
            result.BillingEventsAppended,
            result.RecoveredPaymentAttempts,
            reloadedSubscription.HasDriftDetected,
            reloadedSubscription.DriftDiscrepancies.Length,
            timeProvider.GetUtcNow(),
            null
        );
    }
}
