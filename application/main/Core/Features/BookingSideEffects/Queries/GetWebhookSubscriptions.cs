using JetBrains.Annotations;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.BookingSideEffects.Queries;

[PublicAPI]
public sealed record GetWebhookSubscriptionsQuery(EventTypeId EventTypeId) : IRequest<Result<WebhookSubscriptionsResponse>>;

public sealed class GetWebhookSubscriptionsHandler(
    IWebhookSubscriptionRepository webhookSubscriptionRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetWebhookSubscriptionsQuery, Result<WebhookSubscriptionsResponse>>
{
    public async Task<Result<WebhookSubscriptionsResponse>> Handle(GetWebhookSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWebhooks(executionContext);
        if (!authorization.IsSuccess) return Result<WebhookSubscriptionsResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, query.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<WebhookSubscriptionsResponse>.From(eventType);

        var subscriptions = await webhookSubscriptionRepository.GetForEventTypeAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, query.EventTypeId, cancellationToken);
        return new WebhookSubscriptionsResponse(subscriptions.Select(WebhookSubscriptionResponse.From).ToArray());
    }
}
