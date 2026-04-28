using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record ReconcileTenantBillingCommand(TenantId TenantId) : ICommand, IRequest<Result<ReconcileTenantBillingResponse>>;

[PublicAPI]
public sealed record ReconcileTenantBillingResponse(Guid RunId, BillingReconciliationStatus Status, string Summary);

public sealed class ReconcileTenantBillingHandler(
    ISubscriptionRepository subscriptionRepository,
    IPayFastClient payFastClient,
    AccountDbContext dbContext,
    TimeProvider timeProvider
) : IRequestHandler<ReconcileTenantBillingCommand, Result<ReconcileTenantBillingResponse>>
{
    public async Task<Result<ReconcileTenantBillingResponse>> Handle(ReconcileTenantBillingCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var run = BillingReconciliationRun.Start(command.TenantId, now);
        dbContext.Set<BillingReconciliationRun>().Add(run);

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (subscription is null)
        {
            run.Complete(BillingReconciliationStatus.Failed, "No local subscription exists for this tenant.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        if (subscription.PayFastToken is null)
        {
            run.Complete(BillingReconciliationStatus.Matched, "Local subscription has no PayFast token to reconcile.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        var providerSubscription = await payFastClient.FetchSubscriptionAsync(subscription.PayFastToken, cancellationToken);
        if (providerSubscription is null)
        {
            run.Complete(BillingReconciliationStatus.Failed, "PayFast subscription state could not be fetched.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        var providerStatus = providerSubscription.Status.ToLowerInvariant();
        var providerIsActive = providerStatus is "active" or "success" or "complete";
        var providerIsCancelled = providerStatus.Contains("cancel", StringComparison.OrdinalIgnoreCase);

        if (providerIsActive && subscription.Status == SubscriptionStatus.PastDue)
        {
            subscription.ClearPaymentFailure();
            subscription.RenewBillingPeriod(now);
            subscriptionRepository.Update(subscription);
            run.Complete(BillingReconciliationStatus.Corrected, "Local past-due subscription was corrected to active from PayFast state.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        if (providerIsCancelled && subscription.Status != SubscriptionStatus.Cancelled)
        {
            run.Complete(BillingReconciliationStatus.NeedsManualReview, "PayFast reports a cancelled token while the local subscription is not cancelled.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        var latestPaymentMissing = providerSubscription.LatestPaymentId is not null &&
                                   subscription.PaymentTransactions.All(t => t.ProviderPaymentId != providerSubscription.LatestPaymentId);
        if (latestPaymentMissing && providerSubscription.Amount is not null)
        {
            var importedTransaction = new PaymentTransaction(
                PaymentTransactionId.NewId(),
                providerSubscription.Amount.Value,
                SubscriptionPlanPricing.Currency,
                PaymentTransactionStatus.Succeeded,
                providerSubscription.NextRunDate ?? now,
                null,
                null,
                null,
                "PayFast",
                providerSubscription.LatestPaymentId,
                null,
                providerSubscription.Status
            );
            subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(importedTransaction));
            subscriptionRepository.Update(subscription);
            run.Complete(BillingReconciliationStatus.Corrected, "Missing local PayFast transaction was imported from reconciliation.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        if (latestPaymentMissing)
        {
            run.Complete(BillingReconciliationStatus.NeedsManualReview, "PayFast reports a latest payment id that is not present locally.", now);
            return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
        }

        run.Complete(BillingReconciliationStatus.Matched, "Local billing state matches PayFast state.", now);
        return new ReconcileTenantBillingResponse(run.Id, run.Status, run.Summary);
    }
}
