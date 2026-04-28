using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record RetryPendingInvoicePaymentCommand : ICommand, IRequest<Result<RetryPendingInvoicePaymentResponse>>;

[PublicAPI]
public sealed record RetryPendingInvoicePaymentResponse(bool Paid, string? Uuid);

public sealed class RetryPendingInvoicePaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<RetryPendingInvoicePaymentCommand, Result<RetryPendingInvoicePaymentResponse>>
{
    public async Task<Result<RetryPendingInvoicePaymentResponse>> Handle(RetryPendingInvoicePaymentCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<RetryPendingInvoicePaymentResponse>.Forbidden("Only owners can retry payments.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status != SubscriptionStatus.PastDue)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("There is no pending invoice to retry.");
        }

        if (subscription.PayFastToken is null)
        {
            return Result<RetryPendingInvoicePaymentResponse>.BadRequest("No payment method on file. Please update the card first.");
        }

        var plan = subscription.Plan;
        var amount = SubscriptionPlanPricing.GetMonthlyPrice(plan);
        var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken, amount, $"Nerova Bookings {plan} Plan — recovery", cancellationToken);

        if (!charged)
        {
            return new RetryPendingInvoicePaymentResponse(false, null);
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
        subscription.ClearPaymentFailure();

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        if (tenant is not null && tenant.State == TenantState.Suspended && tenant.SuspensionReason == SuspensionReason.PaymentFailed)
        {
            tenant.Activate();
            tenantRepository.Update(tenant);
        }

        events.CollectEvent(new PendingInvoicePaymentRetried(subscription.Id));
        events.CollectEvent(new PaymentRecovered(subscription.Id, plan, daysInPastDue, amount, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);

        return new RetryPendingInvoicePaymentResponse(true, null);
    }
}
