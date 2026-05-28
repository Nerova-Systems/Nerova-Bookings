using SharedKernel.Authentication;

namespace Main.Features.Webhooks.Shared;

/// <summary>
///     Authorization helpers for the webhook platform. Following the workflows pattern: only
///     Owner/Admin roles may manage webhooks, and access is gated by the <c>cap-webhooks</c>
///     feature flag so SaaS tiers can roll the surface out gradually.
/// </summary>
public static class WebhookAuthorization
{
    public const string ManageWebhooksForbiddenMessage = "Only owners and admins can manage webhooks.";

    public const string WebhooksFeatureDisabledMessage = "The webhooks feature is not enabled for your account.";

    public const string WebhooksFeatureFlagKey = "cap-webhooks";

    public static bool CanManageWebhooks(UserInfo userInfo)
    {
        return userInfo.Role is "Owner" or "Admin";
    }
}
