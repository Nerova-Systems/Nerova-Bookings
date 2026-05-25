using Main.Features.Webhooks.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Webhooks.Commands;

/// <summary>
///     Common pre-flight check shared by every webhook command: authentication, Owner/Admin role,
///     and the <c>cap-webhooks</c> feature flag. Keeps the per-handler boilerplate to one line.
/// </summary>
internal readonly struct WebhookAccessGate
{
    public WebhookAccessGate(TenantId tenantId, UserId userId)
    {
        TenantId = tenantId;
        UserId = userId;
        Failure = null;
    }

    private WebhookAccessGate(Result failure)
    {
        TenantId = new TenantId(0);
        UserId = new UserId(string.Empty);
        Failure = failure;
    }

    public TenantId TenantId { get; }

    public UserId UserId { get; }

    public Result? Failure { get; }

    public static WebhookAccessGate<TResponse> Evaluate<TResponse>(IExecutionContext executionContext)
    {
        var userInfo = executionContext.UserInfo;
        var tenantId = executionContext.TenantId;
        var userId = userInfo.Id;

        if (tenantId is null || userId is null)
        {
            return WebhookAccessGate<TResponse>.Fail(Result<TResponse>.Unauthorized("Authentication is required."));
        }

        if (!WebhookAuthorization.CanManageWebhooks(userInfo))
        {
            return WebhookAccessGate<TResponse>.Fail(Result<TResponse>.Forbidden(WebhookAuthorization.ManageWebhooksForbiddenMessage));
        }

        if (!userInfo.IsFeatureFlagEnabled(WebhookAuthorization.WebhooksFeatureFlagKey))
        {
            return WebhookAccessGate<TResponse>.Fail(Result<TResponse>.Forbidden(WebhookAuthorization.WebhooksFeatureDisabledMessage));
        }

        return WebhookAccessGate<TResponse>.Ok(tenantId, userId);
    }

    public static WebhookAccessGate EvaluateNonGeneric(IExecutionContext executionContext)
    {
        var userInfo = executionContext.UserInfo;
        var tenantId = executionContext.TenantId;
        var userId = userInfo.Id;

        if (tenantId is null || userId is null) return new WebhookAccessGate(Result.Unauthorized("Authentication is required."));
        if (!WebhookAuthorization.CanManageWebhooks(userInfo)) return new WebhookAccessGate(Result.Forbidden(WebhookAuthorization.ManageWebhooksForbiddenMessage));
        if (!userInfo.IsFeatureFlagEnabled(WebhookAuthorization.WebhooksFeatureFlagKey))
        {
            return new WebhookAccessGate(Result.Forbidden(WebhookAuthorization.WebhooksFeatureDisabledMessage));
        }

        return new WebhookAccessGate(tenantId, userId);
    }
}

internal readonly struct WebhookAccessGate<TResponse>
{
    private WebhookAccessGate(TenantId tenantId, UserId userId, Result<TResponse>? failure)
    {
        TenantId = tenantId;
        UserId = userId;
        Failure = failure;
    }

    public TenantId TenantId { get; }

    public UserId UserId { get; }

    public Result<TResponse>? Failure { get; }

    public static WebhookAccessGate<TResponse> Ok(TenantId tenantId, UserId userId) => new(tenantId, userId, null);

    public static WebhookAccessGate<TResponse> Fail(Result<TResponse> failure) => new(new TenantId(0), new UserId(string.Empty), failure);
}
