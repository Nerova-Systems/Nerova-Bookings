using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Account.Features.Tenants.BackOffice.Commands;

[PublicAPI]
public sealed record CancelBackOfficeSubscriptionCommand : ICommand, IRequest<Result>
{
    [JsonIgnore]
    public TenantId TenantId { get; init; } = null!;
}

public sealed class CancelBackOfficeSubscriptionHandler(
    ITenantRepository tenantRepository,
    ISubscriptionRepository subscriptionRepository,
    IBillingEventRepository billingEventRepository,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<CancelBackOfficeSubscriptionHandler> logger
) : IRequestHandler<CancelBackOfficeSubscriptionCommand, Result>
{
    public async Task<Result> Handle(CancelBackOfficeSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (tenant is null) return Result.NotFound($"Tenant with id '{command.TenantId}' not found.");

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (subscription is null) return Result.NotFound($"Subscription for tenant '{command.TenantId}' not found.");

        if (subscription.Plan == SubscriptionPlan.Basis)
        {
            return Result.BadRequest("Cannot cancel a Basis subscription.");
        }

        if (subscription.PaystackAuthorizationCode is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result.BadRequest("No active Paystack authorization found.");
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return Result.BadRequest("Subscription is already scheduled for cancellation.");
        }

        var now = timeProvider.GetUtcNow();
        var reason = CancellationReason.CancelledByAdmin;
        subscription.SetCancellation(true, reason, null);
        subscriptionRepository.Update(subscription);

        var priceAmount = subscription.CurrentPriceAmount ?? 0m;
        var currency = subscription.CurrentPriceCurrency;
        var billingEvent = BillingEvent.Create(
            subscription.TenantId,
            subscription.Id,
            $"paystack:{subscription.Id}:admin-cancel:{now.ToUnixTimeMilliseconds()}",
            BillingEventType.SubscriptionCancelled,
            now,
            0m,
            subscription.Plan,
            subscription.Plan,
            priceAmount,
            0m,
            -priceAmount,
            currency,
            reason
        );
        await billingEventRepository.AddAsync(billingEvent, cancellationToken);

        int? daysUntilExpiry = subscription.CurrentPeriodEnd is null ? null : Math.Max(0, (subscription.CurrentPeriodEnd.Value - now).Days);
        var daysOnCurrentPlan = subscription.CurrentPeriodStart is null ? 0 : Math.Max(0, (now - subscription.CurrentPeriodStart.Value).Days);
        events.CollectEvent(new SubscriptionCancelled(subscription.Id, subscription.Plan, reason, daysUntilExpiry, daysOnCurrentPlan, priceAmount, -priceAmount, currency ?? "unknown"));

        return Result.Success();
    }
}
