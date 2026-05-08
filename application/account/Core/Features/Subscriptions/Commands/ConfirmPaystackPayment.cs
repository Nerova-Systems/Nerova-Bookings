using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ConfirmPaystackPaymentCommand(string Reference, SubscriptionPlan Plan, PaystackPaymentPurpose Purpose)
    : ICommand, IRequest<Result<ConfirmPaystackPaymentResponse>>;

[PublicAPI]
public sealed record ConfirmPaystackPaymentResponse(bool Paid);

public sealed class ConfirmPaystackPaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<ConfirmPaystackPaymentCommand, Result<ConfirmPaystackPaymentResponse>>
{
    public async Task<Result<ConfirmPaystackPaymentResponse>> Handle(ConfirmPaystackPaymentCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<ConfirmPaystackPaymentResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        if (command.Plan == SubscriptionPlan.Basis)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Cannot confirm payment for the Basis plan.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        if (subscription.PaystackCustomerId is null)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Billing information must be saved before payment confirmation.");
        }

        var verified = await paystackClientFactory.GetClient().VerifyTransactionAsync(command.Reference, command.Purpose, cancellationToken);
        if (verified?.Paid != true)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest(verified?.ErrorMessage ?? "Paystack payment could not be verified.");
        }

        if (verified.CustomerId is not null && verified.CustomerId != subscription.PaystackCustomerId)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment customer does not match this subscription.");
        }

        if (verified.Authorization is null)
        {
            return Result<ConfirmPaystackPaymentResponse>.BadRequest("Paystack payment did not return a reusable card authorization.");
        }

        var previousPlan = subscription.Plan;
        var now = timeProvider.GetUtcNow();
        subscription.SetPaystackAuthorization(verified.Authorization.AuthorizationCode, verified.Authorization.Email, verified.Authorization.Signature, verified.PaymentMethod);
        subscription.SetPaystackSubscription(verified.Authorization.AuthorizationCode, command.Plan, verified.Amount, verified.Currency, now.AddMonths(1), verified.PaymentMethod);
        subscription.ClearPaymentFailure();
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), verified.Amount, verified.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        if (tenant is not null)
        {
            tenant.UpdatePlan(command.Plan);
            tenant.Activate();
            tenantRepository.Update(tenant);
        }

        subscriptionRepository.Update(subscription);

        if (previousPlan == SubscriptionPlan.Basis)
        {
            events.CollectEvent(new SubscriptionCreated(subscription.Id, command.Plan, verified.Amount, verified.Amount, verified.Currency));
        }
        else if (command.Plan.IsUpgradeFrom(previousPlan))
        {
            events.CollectEvent(new SubscriptionUpgraded(subscription.Id, previousPlan, command.Plan, 0, subscription.CurrentPriceAmount ?? verified.Amount, verified.Amount, verified.Amount - (subscription.CurrentPriceAmount ?? 0m), verified.Currency));
        }

        return new ConfirmPaystackPaymentResponse(true);
    }
}
