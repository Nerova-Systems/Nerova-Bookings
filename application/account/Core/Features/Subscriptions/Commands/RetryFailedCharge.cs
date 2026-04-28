using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record RetryFailedChargeCommand : ICommand, IRequest<Result>;

public sealed class RetryFailedChargeHandler(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<RetryFailedChargeCommand, Result>
{
    public async Task<Result> Handle(RetryFailedChargeCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status != SubscriptionStatus.PastDue)
        {
            return Result.BadRequest("Subscription is not past due. Nothing to retry.");
        }

        if (subscription.PayFastToken is null)
        {
            return Result.BadRequest("No payment method on file. Please reactivate with a new payment method.");
        }

        var amount = SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan);
        var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken, amount, $"Nerova Bookings {subscription.Plan} Plan — retry", cancellationToken);

        if (!charged)
        {
            return Result.BadRequest("Payment retry failed. Please update your payment method and try again.");
        }

        var now = timeProvider.GetUtcNow();
        var daysInPastDue = subscription.FirstPaymentFailedAt.HasValue ? (int)(now - subscription.FirstPaymentFailedAt.Value).TotalDays : 0;

        var transaction = new PaymentTransaction(
            PaymentTransactionId.NewId(),
            amount,
            SubscriptionPlanPricing.Currency,
            PaymentTransactionStatus.Succeeded,
            now,
            null,
            null,
            null
        );
        subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));
        subscription.RenewBillingPeriod(now);

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        if (tenant is not null && tenant.State == TenantState.Suspended && tenant.SuspensionReason == SuspensionReason.PaymentFailed)
        {
            tenant.Activate();
            tenantRepository.Update(tenant);
        }

        events.CollectEvent(new PaymentRecovered(subscription.Id, subscription.Plan, daysInPastDue, amount, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}
