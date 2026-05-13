using System.Data;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record RetryRenewalPaymentCommand : ICommand, IRequest<Result<RetryRenewalPaymentResponse>>;

[PublicAPI]
public sealed record RetryRenewalPaymentResponse(
    bool Paid,
    string? AccessCode,
    string? Reference,
    string? PublicKey,
    decimal? Amount,
    string? Currency,
    string OperationPurpose
);

public sealed class RetryRenewalPaymentHandler(
    AccountDbContext dbContext,
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<RetryRenewalPaymentHandler> logger
) : IRequestHandler<RetryRenewalPaymentCommand, Result<RetryRenewalPaymentResponse>>
{
    public async Task<Result<RetryRenewalPaymentResponse>> Handle(RetryRenewalPaymentCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<RetryRenewalPaymentResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        await using var transaction = isSqlite
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var subscription = await subscriptionRepository.GetCurrentWithLockAsync(cancellationToken);

        if (subscription.PaystackAuthorizationCode is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<RetryRenewalPaymentResponse>.BadRequest("No active Paystack authorization found.");
        }

        if (subscription.FirstPaymentFailedAt is null || subscription.CurrentPriceAmount is null || subscription.CurrentPriceCurrency is null)
        {
            return Result<RetryRenewalPaymentResponse>.BadRequest("No pending renewal payment found for this subscription.");
        }

        var billingEmail = subscription.PaystackAuthorizationEmail ?? subscription.BillingInfo?.Email;
        if (billingEmail is null)
        {
            return Result<RetryRenewalPaymentResponse>.BadRequest("Billing information must include an email before retrying payment.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var charge = await paystackClient.ChargeAuthorizationAsync(
            subscription.PaystackCustomerId!,
            subscription.PaystackAuthorizationCode,
            billingEmail,
            PaystackPaymentPurpose.Retry,
            subscription.Plan,
            subscription.CurrentPriceAmount.Value,
            subscription.CurrentPriceCurrency,
            cancellationToken
        );
        if (charge is null)
        {
            return Result<RetryRenewalPaymentResponse>.BadRequest("Failed to retry renewal payment.");
        }

        var paymentAttempt = PaystackPaymentAttempt.Create(
            subscription.TenantId,
            subscription.Id,
            charge.Reference,
            subscription.PaystackCustomerId!,
            subscription.PaystackAuthorizationCode,
            PaystackPaymentPurpose.Retry,
            subscription.Plan,
            charge.Amount,
            charge.Currency
        );

        var now = timeProvider.GetUtcNow();
        if (!charge.Paid)
        {
            paymentAttempt.MarkFailed(now, charge.ErrorMessage ?? "Paystack could not charge the saved payment method.");
            await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<RetryRenewalPaymentResponse>.BadRequest(charge.ErrorMessage ?? "Paystack could not charge the saved payment method.", true);
        }

        var nextBillingAt = now.AddMonths(1);
        paymentAttempt.MarkSucceeded(now);
        subscription.ClearPaymentFailure();
        subscription.StartBillingPeriod(subscription.Plan, charge.Amount, charge.Currency, now, nextBillingAt, charge.PaymentMethod);
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), charge.Amount, charge.Amount, 0m, charge.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );
        subscriptionRepository.Update(subscription);
        await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
        events.CollectEvent(new RenewalPaymentRetried(subscription.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RetryRenewalPaymentResponse(true, null, charge.Reference, null, charge.Amount, charge.Currency, nameof(PaystackPaymentPurpose.Retry));
    }
}
