using System.Globalization;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record InitiateSubscriptionCommand(SubscriptionPlan Plan) : ICommand, IRequest<Result<InitiateSubscriptionResponse>>;

[PublicAPI]
public sealed record InitiateSubscriptionResponse(string Uuid);

public sealed class InitiateSubscriptionValidator : AbstractValidator<InitiateSubscriptionCommand>
{
    public InitiateSubscriptionValidator()
    {
        RuleFor(x => x.Plan).NotEqual(SubscriptionPlan.Trial).WithMessage("Cannot initiate checkout for the Trial plan.");
    }
}

public sealed class InitiateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    IOptions<PayFastSettings> payFastOptions,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<InitiateSubscriptionCommand, Result<InitiateSubscriptionResponse>>
{
    public async Task<Result<InitiateSubscriptionResponse>> Handle(InitiateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<InitiateSubscriptionResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status == SubscriptionStatus.Active)
        {
            return Result<InitiateSubscriptionResponse>.BadRequest("Subscription is already active. Use upgrade or downgrade instead.");
        }

        if (subscription.Status == SubscriptionStatus.Expired)
        {
            return Result<InitiateSubscriptionResponse>.BadRequest("Subscription has expired. Please contact support.");
        }

        var settings = payFastOptions.Value;
        var amount = SubscriptionPlanPricing.GetMonthlyPrice(command.Plan);

        // Use PayFast Tokenization (subscription_type=2) instead of Subscriptions (=1).
        // With Tokenization, PayFast stores the card and we own all billing — adhoc charges via
        // /subscriptions/{token}/adhoc, no PayFast-managed recurring schedule. This avoids the
        // double-billing race we saw under subscription_type=1 where PayFast auto-bills on
        // billing_date in addition to our BillingJob. Tokens appear on the Tokenization page.
        var parameters = new Dictionary<string, string>
        {
            { "merchant_id", settings.MerchantId },
            { "merchant_key", settings.MerchantKey },
            { "return_url", settings.ReturnUrl },
            { "cancel_url", settings.CancelUrl },
            { "notify_url", settings.NotifyUrl },
            // Buyer name shown in PayFast dashboard. We use the tenant name as the surname so the
            // dashboard makes the organisation immediately identifiable (e.g. "Colin / Nerova Test")
            // instead of every test charge being lumped under a generic "Test Buyer".
            { "name_first", executionContext.UserInfo.FirstName ?? "Customer" },
            { "name_last", executionContext.UserInfo.TenantName ?? executionContext.UserInfo.LastName ?? "" },
            // In PayFast sandbox, the buyer's email_address cannot equal the merchant account email
            // (the sandbox account is registered with the developer's email). Use a per-tenant test
            // buyer email so each tenant shows up as a distinct party in the PayFast dashboard rather
            // than being merged under one shared "Test Buyer". In production, settings.Sandbox is
            // false and the real user email is used.
            { "email_address", settings.Sandbox ? $"buyer-{executionContext.TenantId}@nerova.test" : (executionContext.UserInfo.Email ?? "") },
            { "m_payment_id", Guid.NewGuid().ToString("N") },
            { "amount", amount.ToString("F2", CultureInfo.InvariantCulture) },
            { "item_name", $"Nerova Bookings {command.Plan} Plan" },
            { "custom_str1", subscription.Id.ToString() },
            { "custom_str2", executionContext.TenantId!.ToString()! },
            { "custom_str3", command.Plan.ToString() },
            { "subscription_type", "2" }
        };

        parameters["signature"] = PayFastSignature.Generate(parameters, settings.Passphrase);

        var uuid = await payFastClient.ProcessOnsitePaymentAsync(parameters, cancellationToken);

        if (uuid is null)
        {
            return Result<InitiateSubscriptionResponse>.BadRequest("Failed to initiate payment. Please try again.");
        }

        events.CollectEvent(new SubscriptionCheckoutStarted(subscription.Id, command.Plan, false));

        return new InitiateSubscriptionResponse(uuid);
    }
}
