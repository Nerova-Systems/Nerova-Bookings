using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ConfirmSubscriptionCheckoutCommand(string Reference) : ICommand, IRequest<Result>;

public sealed class ConfirmSubscriptionCheckoutValidator : AbstractValidator<ConfirmSubscriptionCheckoutCommand>
{
    public ConfirmSubscriptionCheckoutValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Payment reference is required.");
    }
}

public sealed class ConfirmSubscriptionCheckoutHandler(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext
) : IRequestHandler<ConfirmSubscriptionCheckoutCommand, Result>
{
    public async Task<Result> Handle(ConfirmSubscriptionCheckoutCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        if (subscription.PaystackCustomerId is null)
        {
            return Result.BadRequest("No Paystack customer found. A subscription must be created first.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var checkoutSubscriptionId = await paystackClient.GetCheckoutSessionSubscriptionIdAsync(command.Reference, cancellationToken);
        PaymentMethod? verifiedPaymentMethod = null;
        if (checkoutSubscriptionId is null)
        {
            verifiedPaymentMethod = await paystackClient.GetPaymentMethodFromTransactionAsync(command.Reference, cancellationToken);
        }

        if (checkoutSubscriptionId is null && verifiedPaymentMethod is null)
        {
            return Result.BadRequest("Payment reference has not been verified by Paystack.");
        }

        var paystackState = await paystackClient.SyncSubscriptionStateAsync(subscription.PaystackCustomerId, cancellationToken);
        if (paystackState?.PaystackSubscriptionId is null)
        {
            return Result.BadRequest("Subscription has not been activated in Paystack.");
        }

        var tenant = await tenantRepository.GetCurrentTenantAsync(cancellationToken);
        if (tenant is null)
        {
            return Result.BadRequest("Tenant not found.");
        }

        subscription.SetPaystackSubscription(
            paystackState.PaystackSubscriptionId,
            paystackState.Plan,
            paystackState.CurrentPriceAmount,
            paystackState.CurrentPriceCurrency,
            paystackState.CurrentPeriodEnd,
            paystackState.PaymentMethod ?? verifiedPaymentMethod
        );
        subscription.SetCancellation(paystackState.CancelAtPeriodEnd, paystackState.CancellationReason, paystackState.CancellationFeedback);

        if (paystackState.PaymentTransactions is not null)
        {
            subscription.SetPaymentTransactions([.. paystackState.PaymentTransactions]);
        }

        tenant.Activate();
        tenant.UpdatePlan(paystackState.Plan);

        subscriptionRepository.Update(subscription);
        tenantRepository.Update(tenant);

        return Result.Success();
    }
}
