using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Validation;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record UpdateBillingInfoCommand(
    string Name,
    string Address,
    string PostalCode,
    string City,
    string? State,
    string Country,
    string Email,
    string? TaxId
)
    : ICommand, IRequest<Result>;

public sealed class UpdateBillingInfoValidator : AbstractValidator<UpdateBillingInfoCommand>
{
    public UpdateBillingInfoValidator()
    {
        RuleFor(x => x.Name).Length(1, 100).WithMessage("Name must be between 1 and 100 characters.");
        RuleFor(x => x.Address).Length(1, 200).WithMessage("Address must be between 1 and 200 characters.");
        RuleFor(x => x.PostalCode).Length(1, 10).WithMessage("Postal code must be between 1 and 10 characters.");
        RuleFor(x => x.City).Length(1, 50).WithMessage("City must be between 1 and 50 characters.");
        RuleFor(x => x.State).MaximumLength(50).WithMessage("State must be no longer than 50 characters.");
        RuleFor(x => x.Country).Length(2).WithMessage("Country is required.");
        RuleFor(x => x.Email).SetValidator(new SharedValidations.Email());
        RuleFor(x => x.TaxId).MaximumLength(20).WithMessage("Tax ID must be no longer than 20 characters.");
    }
}

public sealed class UpdateBillingInfoHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext
) : IRequestHandler<UpdateBillingInfoCommand, Result>
{
    public async Task<Result> Handle(UpdateBillingInfoCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage billing information.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        var paystackClient = paystackClientFactory.GetClient();

        if (subscription.PaystackCustomerId is null)
        {
            if (executionContext.UserInfo.Email is null)
            {
                return Result.BadRequest("User email is required to create a Paystack customer.");
            }

            var customerId = await paystackClient.CreateCustomerAsync(command.Name, executionContext.UserInfo.Email, subscription.TenantId.Value, cancellationToken);
            if (customerId is null)
            {
                return Result.BadRequest("Failed to create Paystack customer.");
            }

            subscription.SetPaystackCustomerId(customerId);
            subscriptionRepository.Update(subscription);
        }

        var addressLines = command.Address.Split('\n', 2, StringSplitOptions.TrimEntries);
        var line1 = addressLines[0];
        var line2 = addressLines.Length > 1 ? addressLines[1] : null;

        var billingInfo = new BillingInfo(
            command.Name,
            new BillingAddress(line1, line2, command.PostalCode, command.City, command.State, command.Country),
            command.Email,
            command.TaxId
        );

        var success = await paystackClient.UpdateCustomerBillingInfoAsync(subscription.PaystackCustomerId!, billingInfo, executionContext.UserInfo.Locale, cancellationToken);
        if (!success)
        {
            return Result.BadRequest("Failed to update billing information in Paystack.");
        }

        if (command.TaxId != subscription.BillingInfo?.TaxId)
        {
            var taxIdSynced = await paystackClient.SyncCustomerTaxIdAsync(subscription.PaystackCustomerId!, command.TaxId, cancellationToken);
            if (!taxIdSynced)
            {
                return Result.BadRequest("TaxId", "The provided Tax ID is not valid.");
            }
        }

        // Subscription is updated and telemetry is collected in ProcessPendingPaystackEvents when Paystack confirms the state change via webhook

        return Result.Success();
    }
}
