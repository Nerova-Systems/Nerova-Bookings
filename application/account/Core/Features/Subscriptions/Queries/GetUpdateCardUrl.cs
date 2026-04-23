using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Queries;

[PublicAPI]
public sealed record GetUpdateCardUrlQuery : IRequest<Result<UpdateCardUrlResponse>>;

[PublicAPI]
public sealed record UpdateCardUrlResponse(string Url);

public sealed class GetUpdateCardUrlHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient
) : IRequestHandler<GetUpdateCardUrlQuery, Result<UpdateCardUrlResponse>>
{
    public async Task<Result<UpdateCardUrlResponse>> Handle(GetUpdateCardUrlQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<UpdateCardUrlResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PayFastToken is null)
        {
            return Result<UpdateCardUrlResponse>.BadRequest("No payment method on file.");
        }

        var url = payFastClient.GetUpdateCardUrl(subscription.PayFastToken);
        return new UpdateCardUrlResponse(url);
    }
}
