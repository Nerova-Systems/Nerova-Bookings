using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetCheckoutPreviewQuery(SubscriptionPlan Plan) : IRequest<Result<CheckoutPreviewResponse>>;

[PublicAPI]
public sealed record CheckoutPreviewResponse(decimal TotalAmount, string Currency, decimal TaxAmount);

public sealed class GetCheckoutPreviewValidator : AbstractValidator<GetCheckoutPreviewQuery>
{
    public GetCheckoutPreviewValidator()
    {
        RuleFor(x => x.Plan).NotEqual(SubscriptionPlan.Basis).WithMessage("Cannot preview checkout for the Basis plan.");
    }
}

public sealed class GetCheckoutPreviewHandler(ISubscriptionRepository subscriptionRepository, PaystackClientFactory paystackClientFactory, IExecutionContext executionContext)
    : IRequestHandler<GetCheckoutPreviewQuery, Result<CheckoutPreviewResponse>>
{
    public async Task<Result<CheckoutPreviewResponse>> Handle(GetCheckoutPreviewQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<CheckoutPreviewResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackCustomerId is null)
        {
            return Result<CheckoutPreviewResponse>.BadRequest("Billing information must be saved before previewing checkout.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var preview = await paystackClient.GetCheckoutPreviewAsync(subscription.PaystackCustomerId, query.Plan, cancellationToken);
        if (preview is null)
        {
            return Result<CheckoutPreviewResponse>.BadRequest("Failed to get checkout preview from Paystack.");
        }

        return new CheckoutPreviewResponse(preview.TotalAmount, preview.Currency, preview.TaxAmount);
    }
}
