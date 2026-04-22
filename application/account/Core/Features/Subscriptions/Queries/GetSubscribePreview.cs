using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetSubscribePreviewQuery(SubscriptionPlan Plan) : IRequest<Result<SubscribePreviewResponse>>;

[PublicAPI]
public sealed record SubscribePreviewResponse(decimal TotalAmount, string Currency, decimal TaxAmount);

public sealed class GetSubscribePreviewValidator : AbstractValidator<GetSubscribePreviewQuery>
{
    public GetSubscribePreviewValidator()
    {
        RuleFor(x => x.Plan).NotEqual(SubscriptionPlan.Trial).WithMessage("Cannot preview subscription for the Trial plan.");
    }
}

public sealed class GetSubscribePreviewHandler(ISubscriptionRepository subscriptionRepository, IExecutionContext executionContext)
    : IRequestHandler<GetSubscribePreviewQuery, Result<SubscribePreviewResponse>>
{
    public async Task<Result<SubscribePreviewResponse>> Handle(GetSubscribePreviewQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<SubscribePreviewResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        _ = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        // TODO: Implement PayFast subscribe preview in pf-03
        return Result<SubscribePreviewResponse>.BadRequest("Subscribe preview not yet available.");
    }
}
