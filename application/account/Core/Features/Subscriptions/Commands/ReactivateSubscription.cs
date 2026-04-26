using System.Globalization;
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

        var now = timeProvider.GetUtcNow();
        var plan = subscription.Plan;
        var amount = plan == SubscriptionPlan.Trial ? 0m : SubscriptionPlanPricing.GetMonthlyPrice(plan);
        var daysSinceCancelled = subscription.CancelledAt.HasValue ? (int)(now - subscription.CancelledAt.Value).TotalDays : 0;

        // Case 1: Still inside the paid billing period — user already paid, just clear cancellation.
        // No charge, no PayFast call, no checkout. Period end and token are preserved.
        if (subscription.CurrentPeriodEnd is not null && subscription.CurrentPeriodEnd > now)
        {
            subscription.ResumeWithinPaidPeriod();
            events.CollectEvent(new SubscriptionReactivated(subscription.Id, plan, null, daysSinceCancelled, amount, amount, SubscriptionPlanPricing.Currency));
            subscriptionRepository.Update(subscription);
            return new ReactivateSubscriptionResponse(null);
        }

        events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, plan, subscription.PayFastToken is not null));

        // Case 2: Period has lapsed but a token exists — charge the token to start a new period.
        if (subscription.PayFastToken is not null)
        {
            var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken, amount, $"Nerova Bookings {plan} Plan — reactivation", cancellationToken);

            if (charged)
            {
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

        // Case 3: No token or charge failed — start a fresh checkout with the onsite lightbox.
        var settings = payFastOptions.Value;
        var reactivatePlan = plan == SubscriptionPlan.Trial ? SubscriptionPlan.Starter : plan;
        var reactivateAmount = SubscriptionPlanPricing.GetMonthlyPrice(reactivatePlan);
        var billingDay = now.Day <= 28 ? now.Day : 1;

        var parameters = new Dictionary<string, string>
        {
            { "merchant_id", settings.MerchantId },
            { "merchant_key", settings.MerchantKey },
            { "return_url", settings.ReturnUrl },
            { "cancel_url", settings.CancelUrl },
            { "notify_url", settings.NotifyUrl },
            { "name_first", executionContext.UserInfo.FirstName ?? "Customer" },
            // Sandbox PayFast rejects when buyer email == merchant email; use a test buyer in sandbox
            { "email_address", settings.Sandbox ? "buyer@nerova.test" : (executionContext.UserInfo.Email ?? "") },
            { "m_payment_id", Guid.NewGuid().ToString("N") },
            { "amount", reactivateAmount.ToString("F2", CultureInfo.InvariantCulture) },
            { "item_name", $"Nerova Bookings {reactivatePlan} Plan" },
            { "subscription_type", "1" },
            { "billing_date", now.ToString("yyyy-MM-", CultureInfo.InvariantCulture) + billingDay.ToString("D2", CultureInfo.InvariantCulture) },
            { "recurring_amount", reactivateAmount.ToString("F2", CultureInfo.InvariantCulture) },
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
