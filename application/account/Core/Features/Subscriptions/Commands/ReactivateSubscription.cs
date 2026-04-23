using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ReactivateSubscriptionCommand : ICommand, IRequest<Result<ReactivateSubscriptionResponse>>;

[PublicAPI]
public sealed record ReactivateSubscriptionResponse(string? Uuid);

public sealed class ReactivateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    IOptions<PayFastSettings> payFastOptions,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<ReactivateSubscriptionCommand, Result<ReactivateSubscriptionResponse>>
{
    public async Task<Result<ReactivateSubscriptionResponse>> Handle(ReactivateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<ReactivateSubscriptionResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status != SubscriptionStatus.Cancelled)
        {
            return Result<ReactivateSubscriptionResponse>.BadRequest("Subscription is not cancelled. Nothing to reactivate.");
        }

        events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, subscription.Plan, subscription.PayFastToken is not null));

        // If a token exists from the previous subscription, attempt to charge it immediately
        if (subscription.PayFastToken is not null)
        {
            var plan = subscription.Plan;
            var amount = SubscriptionPlanPricing.GetMonthlyPrice(plan);
            var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken, amount, $"Nerova Bookings {plan} Plan — reactivation", cancellationToken);

            if (charged)
            {
                var now = timeProvider.GetUtcNow();
                var daysSinceCancelled = subscription.CancelledAt.HasValue ? (int)(now - subscription.CancelledAt.Value).TotalDays : 0;
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
                subscription.Activate(plan, null, subscription.PayFastToken, now);

                events.CollectEvent(new SubscriptionReactivated(subscription.Id, plan, null, daysSinceCancelled, amount, amount, SubscriptionPlanPricing.Currency));

                subscriptionRepository.Update(subscription);
                return new ReactivateSubscriptionResponse(null);
            }
        }

        // No token or charge failed — start a fresh checkout with the onsite lightbox
        var settings = payFastOptions.Value;
        var reactivatePlan = subscription.Plan == SubscriptionPlan.Trial ? SubscriptionPlan.Starter : subscription.Plan;
        var reactivateAmount = SubscriptionPlanPricing.GetMonthlyPrice(reactivatePlan);
        var billingDay = timeProvider.GetUtcNow().Day <= 28 ? timeProvider.GetUtcNow().Day : 1;

        var parameters = new SortedDictionary<string, string>
        {
            { "merchant_id", settings.MerchantId },
            { "merchant_key", settings.MerchantKey },
            { "return_url", settings.ReturnUrl },
            { "cancel_url", settings.CancelUrl },
            { "notify_url", settings.NotifyUrl },
            { "name_first", executionContext.UserInfo.FirstName ?? "Customer" },
            { "email_address", executionContext.UserInfo.Email },
            { "m_payment_id", Guid.NewGuid().ToString("N") },
            { "amount", reactivateAmount.ToString("F2") },
            { "item_name", $"Nerova Bookings {reactivatePlan} Plan" },
            { "subscription_type", "1" },
            { "billing_date", timeProvider.GetUtcNow().ToString("yyyy-MM-") + billingDay.ToString("D2") },
            { "recurring_amount", reactivateAmount.ToString("F2") },
            { "frequency", "3" },
            { "cycles", "0" },
            { "custom_str1", subscription.Id.ToString() },
            { "custom_str2", executionContext.TenantId!.ToString()! },
            { "custom_str3", reactivatePlan.ToString() }
        };
        parameters["signature"] = PayFastSignature.Generate(parameters, settings.Passphrase);

        var uuid = await payFastClient.ProcessOnsitePaymentAsync(parameters, cancellationToken);

        if (uuid is null)
        {
            return Result<ReactivateSubscriptionResponse>.BadRequest("Failed to initiate payment. Please try again.");
        }

        return new ReactivateSubscriptionResponse(uuid);
    }
}
