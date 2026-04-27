using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record UpdateBillingInfoCommand(
    string Name,
    string Line1,
    string? Line2,
    string PostalCode,
    string City,
    string? State,
    string Country,
    string Email,
    string? TaxId
) : ICommand, IRequest<Result>;

public sealed class UpdateBillingInfoValidator : AbstractValidator<UpdateBillingInfoCommand>
{
    public UpdateBillingInfoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name must be between 1 and 200 characters.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200).WithMessage("A valid email is required.");
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(200).WithMessage("Address line 1 must be between 1 and 200 characters.");
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20).WithMessage("Postal code must be between 1 and 20 characters.");
        RuleFor(x => x.City).NotEmpty().MaximumLength(100).WithMessage("City must be between 1 and 100 characters.");
        RuleFor(x => x.Country).NotEmpty().Length(2).WithMessage("Country must be a 2-letter ISO code.");
        RuleFor(x => x.Line2).MaximumLength(200).WithMessage("Address line 2 must be at most 200 characters.");
        RuleFor(x => x.State).MaximumLength(100).WithMessage("State must be at most 100 characters.");
        RuleFor(x => x.TaxId).MaximumLength(50).WithMessage("Tax ID must be at most 50 characters.");
    }
}

public sealed class UpdateBillingInfoHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateBillingInfoCommand, Result>
{
    public async Task<Result> Handle(UpdateBillingInfoCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can update billing information.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        var isFirstTime = subscription.BillingInfo is null;

        var billingInfo = new BillingInfo(
            command.Name.Trim(),
            new BillingAddress(
                command.Line1.Trim(),
                string.IsNullOrWhiteSpace(command.Line2) ? null : command.Line2.Trim(),
                command.PostalCode.Trim(),
                command.City.Trim(),
                string.IsNullOrWhiteSpace(command.State) ? null : command.State.Trim(),
                command.Country.Trim().ToUpperInvariant()
            ),
            command.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(command.TaxId) ? null : command.TaxId.Trim()
        );

        subscription.SetBillingInfo(billingInfo);

        if (isFirstTime)
        {
            events.CollectEvent(new BillingInfoAdded(subscription.Id, billingInfo.Address.Country, billingInfo.Address.PostalCode, billingInfo.Address.City));
        }
        else
        {
            events.CollectEvent(new BillingInfoUpdated(subscription.Id, billingInfo.Address.Country, billingInfo.Address.PostalCode, billingInfo.Address.City));
        }

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}
