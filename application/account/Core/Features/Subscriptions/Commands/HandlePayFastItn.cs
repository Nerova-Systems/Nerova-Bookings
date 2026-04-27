using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record HandlePayFastItnCommand(IReadOnlyDictionary<string, string> FormFields) : ICommand, IRequest<Result>;

public sealed class HandlePayFastItnHandler(
    ISubscriptionRepository subscriptionRepository,
    IOptions<PayFastSettings> payFastOptions,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<HandlePayFastItnHandler> logger
) : IRequestHandler<HandlePayFastItnCommand, Result>
{
    public async Task<Result> Handle(HandlePayFastItnCommand command, CancellationToken cancellationToken)
    {
        var settings = payFastOptions.Value;
        var fields = command.FormFields;

        if (!VerifySignature(fields, settings.Passphrase))
        {
            logger.LogWarning("PayFast ITN signature verification failed");
            return Result.BadRequest("Invalid signature.");
        }

        var paymentStatus = fields.GetValueOrDefault("payment_status");
        var pfPaymentId = fields.GetValueOrDefault("pf_payment_id") ?? "";
        var token = fields.GetValueOrDefault("token");
        var customStr2 = fields.GetValueOrDefault("custom_str2");
        var customStr3 = fields.GetValueOrDefault("custom_str3");

        if (customStr2 is null || !long.TryParse(customStr2, out var tenantIdValue))
        {
            logger.LogWarning("PayFast ITN missing or invalid tenant ID in custom_str2: {Value}", customStr2);
            return Result.BadRequest("Invalid custom data.");
        }

        var tenantId = new TenantId(tenantIdValue);
        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            logger.LogWarning("PayFast ITN for unknown tenant {TenantId}", tenantId);
            return Result.NotFound($"Subscription for tenant '{tenantId}' not found.");
        }

        var now = timeProvider.GetUtcNow();

        if (paymentStatus == "COMPLETE")
        {
            var plan = Enum.TryParse<SubscriptionPlan>(customStr3, out var parsedPlan) ? parsedPlan : subscription.Plan;
            var price = SubscriptionPlanPricing.GetMonthlyPrice(plan);
            var wasAlreadyActive = subscription.Status == SubscriptionStatus.Active;
            var previousStatus = subscription.Status;
            var daysSinceCancelled = subscription.CancelledAt.HasValue ? (int)(now - subscription.CancelledAt.Value).TotalDays : 0;

            var transaction = new PaymentTransaction(
                PaymentTransactionId.NewId(),
                price,
                SubscriptionPlanPricing.Currency,
                PaymentTransactionStatus.Succeeded,
                now,
                null,
                null,
                null
            );
            subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));

            // PayFast does not return card details from the recurring API, so we surface a generic
            // "Card on file" badge once a token is on file. The Update button in the UI links to
            // PayFast's hosted /eng/recurring/update/{token} page where users manage the actual card.
            if (token is not null && subscription.PaymentMethod is null)
            {
                subscription.SetPaymentMethod(new PaymentMethod("Card on file", null, null, null));
            }

            if (wasAlreadyActive)
            {
                subscription.RenewBillingPeriod(now);
                events.CollectEvent(new SubscriptionRenewed(subscription.Id, plan, price, price, SubscriptionPlanPricing.Currency));
            }
            else
            {
                subscription.Activate(plan, token, pfPaymentId, now);

                if (previousStatus == SubscriptionStatus.Cancelled)
                {
                    events.CollectEvent(new SubscriptionReactivated(subscription.Id, plan, null, daysSinceCancelled, price, price, SubscriptionPlanPricing.Currency));
                }
                else
                {
                    events.CollectEvent(new SubscriptionCreated(subscription.Id, plan, price, price, SubscriptionPlanPricing.Currency));
                }
            }

            subscriptionRepository.Update(subscription);
            return Result.Success();
        }

        if (paymentStatus == "FAILED")
        {
            var plan = subscription.Plan;
            var price = plan != SubscriptionPlan.Trial ? SubscriptionPlanPricing.GetMonthlyPrice(plan) : 0;

            var transaction = new PaymentTransaction(
                PaymentTransactionId.NewId(),
                price,
                SubscriptionPlanPricing.Currency,
                PaymentTransactionStatus.Failed,
                now,
                "Payment declined by PayFast.",
                null,
                null
            );
            subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));
            subscription.SetPastDue(now);

            events.CollectEvent(new PaymentFailed(subscription.Id, plan, price, SubscriptionPlanPricing.Currency));

            subscriptionRepository.Update(subscription);
            return Result.Success();
        }

        logger.LogInformation("PayFast ITN received with status {PaymentStatus} — no state change", paymentStatus);
        return Result.Success();
    }

    private static bool VerifySignature(IReadOnlyDictionary<string, string> fields, string passphrase)
    {
        var receivedSignature = fields.GetValueOrDefault("signature");
        if (receivedSignature is null) return false;

        var expectedSignature = PayFastSignature.GenerateForItn(fields, passphrase);
        return string.Equals(expectedSignature, receivedSignature, StringComparison.OrdinalIgnoreCase);
    }
}
