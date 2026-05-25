using JetBrains.Annotations;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Webhooks.Queries;

[PublicAPI]
public sealed record ListWebhooksQuery : IRequest<Result<WebhooksResponse>>;

public sealed class ListWebhooksHandler(
    IWebhookRepository webhookRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListWebhooksQuery, Result<WebhooksResponse>>
{
    public async Task<Result<WebhooksResponse>> Handle(ListWebhooksQuery query, CancellationToken cancellationToken)
    {
        _ = query;
        var userInfo = executionContext.UserInfo;
        if (userInfo.Id is null) return Result<WebhooksResponse>.Unauthorized("Authentication is required.");
        if (!WebhookAuthorization.CanManageWebhooks(userInfo))
        {
            return Result<WebhooksResponse>.Forbidden(WebhookAuthorization.ManageWebhooksForbiddenMessage);
        }

        if (!userInfo.IsFeatureFlagEnabled(WebhookAuthorization.WebhooksFeatureFlagKey))
        {
            return Result<WebhooksResponse>.Forbidden(WebhookAuthorization.WebhooksFeatureDisabledMessage);
        }

        var webhooks = await webhookRepository.GetForTenantAsync(cancellationToken);
        return new WebhooksResponse(webhooks.Select(WebhookResponse.From).ToArray());
    }
}
