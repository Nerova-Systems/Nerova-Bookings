using Account.Features.Subscriptions.Domain;
using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Commands;

[PublicAPI]
public sealed record LinkWabaAccountCommand(TenantId TenantId, string WabaId, string PhoneNumberId, string DisplayPhoneNumber)
    : ICommand, IRequest<Result>;

public sealed class LinkWabaAccountHandler(
    IWabaConfigurationRepository repository,
    ISubscriptionRepository subscriptionRepository
) : IRequestHandler<LinkWabaAccountCommand, Result>
{
    public async Task<Result> Handle(LinkWabaAccountCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByTenantIdAsync(command.TenantId, cancellationToken);

        // ─── Phone-number limit (per WhatsApp Flows tier) ────────────────
        // The WABA row is 1-per-tenant in this schema, so the only operation that increases the
        // tenant's phone-number count is *switching* the PhoneNumberId on an existing row to one
        // they haven't used before. We reject that switch when the tenant's tier allows only 1
        // phone number. Initial linking (existing == null) is always allowed.
        if (existing is not null
            && !string.Equals(existing.PhoneNumberId, command.PhoneNumberId, StringComparison.Ordinal))
        {
            var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
            var phoneNumberLimit = PhoneNumberLimitForPlan(subscription?.Plan);
            if (phoneNumberLimit != -1 && phoneNumberLimit <= 1)
            {
                return Result.Forbidden("Switching WhatsApp phone numbers requires a higher subscription tier.");
            }
        }

        if (existing is null)
        {
            var config = WabaConfiguration.Create(
                command.TenantId,
                command.WabaId,
                command.PhoneNumberId,
                command.DisplayPhoneNumber
            );
            await repository.AddAsync(config, cancellationToken);
        }
        else
        {
            existing.LinkWaba(command.WabaId, command.PhoneNumberId, command.DisplayPhoneNumber);
            repository.Update(existing);
        }

        return Result.Success();
    }

    /// <summary>
    ///     Mirrors <c>TierLimits.MultiplePhoneNumbers</c> in the main SCS. Kept inline to avoid a
    ///     cross-SCS dependency from account → main; the canonical table lives in the main SCS's
    ///     <c>TierLimits</c>.
    /// </summary>
    private static int PhoneNumberLimitForPlan(SubscriptionPlan? plan)
    {
        return plan switch
        {
            null => 1,
            SubscriptionPlan.Basis => 1,
            SubscriptionPlan.Standard => 3,
            SubscriptionPlan.Premium => -1,
            _ => 1
        };
    }
}
