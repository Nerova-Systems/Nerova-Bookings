using System.Collections.Immutable;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Telemetry;

namespace Account.Features.BackOffice.Invoices.Commands;

[PublicAPI]
public sealed record RefundBackOfficeInvoiceCommand(PaymentTransactionId Id) : ICommand, IRequest<Result>;

public sealed class RefundBackOfficeInvoiceHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    IBillingEventRepository billingEventRepository,
    PaystackClientFactory paystackClientFactory,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<RefundBackOfficeInvoiceCommand, Result>
{
    public async Task<Result> Handle(RefundBackOfficeInvoiceCommand command, CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.GetAllWithTransactionsUnfilteredAsync(cancellationToken);
        var subscription = subscriptions.SingleOrDefault(s => s.PaymentTransactions.Any(t => t.Id == command.Id));
        if (subscription is null) return Result.NotFound($"Invoice with id '{command.Id}' not found.");

        var paymentTransaction = subscription.PaymentTransactions.Single(t => t.Id == command.Id);
        if (paymentTransaction.Status == PaymentTransactionStatus.Refunded || paymentTransaction.RefundedAt is not null || paymentTransaction.CreditNoteUrl is not null)
        {
            return Result.BadRequest("Invoice is already refunded.");
        }

        if (paymentTransaction.Status != PaymentTransactionStatus.Succeeded)
        {
            return Result.BadRequest("Only paid invoices can be refunded.");
        }

        var paystackReference = await ResolvePaystackReferenceAsync(subscription, paymentTransaction, cancellationToken);
        if (paystackReference is null)
        {
            return Result.BadRequest("Invoice does not have a Paystack transaction reference.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var refund = await paystackClient.CreateRefundAsync(paystackReference, paymentTransaction.Amount, paymentTransaction.Currency, cancellationToken);
        if (refund is null)
        {
            return Result.BadRequest("Failed to refund invoice in Paystack.", true);
        }

        var now = timeProvider.GetUtcNow();
        var refundedTransaction = paymentTransaction with
        {
            Status = PaymentTransactionStatus.Refunded,
            RefundedAt = now,
            PaystackReference = paystackReference
        };
        var paymentTransactions = subscription.PaymentTransactions
            .Select(t => t.Id == command.Id ? refundedTransaction : t)
            .ToImmutableArray();
        subscription.SetPaymentTransactions(paymentTransactions);
        subscriptionRepository.Update(subscription);

        var plan = paymentTransaction.Plan ?? subscription.Plan;
        var committedMrr = subscription.CancelAtPeriodEnd ? 0m : subscription.CurrentPriceAmount ?? 0m;
        var billingEvent = BillingEvent.Create(
            subscription.TenantId,
            subscription.Id,
            $"paystack:{paystackReference}:Refund",
            BillingEventType.PaymentRefunded,
            now,
            committedMrr,
            plan,
            plan,
            currency: refund.Currency
        );
        await billingEventRepository.AddAsync(billingEvent, cancellationToken);

        var refundCount = paymentTransactions.Count(t => t.Status == PaymentTransactionStatus.Refunded || t.RefundedAt is not null);
        events.CollectEvent(new PaymentRefunded(subscription.Id, plan, refundCount, refund.Amount, refund.Currency));

        return Result.Success();
    }

    private async Task<string?> ResolvePaystackReferenceAsync(Subscription subscription, PaymentTransaction paymentTransaction, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(paymentTransaction.PaystackReference))
        {
            return paymentTransaction.PaystackReference;
        }

        var attempts = await paystackPaymentAttemptRepository.GetSucceededBySubscriptionIdUnfilteredAsync(subscription.Id, cancellationToken);
        var matchingAttempts = attempts
            .Where(a => a.Purpose != PaystackPaymentPurpose.PaymentMethodAuthorization)
            .Where(a => a.MatchesAmount(paymentTransaction.Amount, paymentTransaction.Currency))
            .Where(a => paymentTransaction.Plan is null || a.Plan == paymentTransaction.Plan)
            .ToArray();

        return matchingAttempts.Length == 1 ? matchingAttempts[0].PaystackReference : null;
    }
}
