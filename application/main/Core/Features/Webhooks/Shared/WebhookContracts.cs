using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Webhooks.Domain;
using SharedKernel.Domain;

namespace Main.Features.Webhooks.Shared;

[PublicAPI]
public sealed record WebhookResponse(
    WebhookId Id,
    UserId? UserId,
    EventTypeId? EventTypeId,
    string TargetUrl,
    string Secret,
    WebhookEventType[] EventSubscriptions,
    bool Active,
    DateTimeOffset CreatedAt
)
{
    /// <summary>Prefix shown on masked webhook secrets. Matches cal.com convention.</summary>
    public const string SecretMaskPrefix = "whsec_";

    /// <summary>Filler glyph used between the prefix and the last 4 chars of the secret.</summary>
    public const string SecretMaskFiller = "................................";

    /// <summary>
    ///     Projects a <see cref="Webhook" /> to its response shape. The shared secret is only
    ///     returned in cleartext when <paramref name="revealSecret" /> is true — on every other
    ///     read it is rendered as <c>whsec_…-{last4}</c> so the value cannot be lifted from the
    ///     transport. Matches cal.com: cleartext on create + explicit regenerate only.
    /// </summary>
    public static WebhookResponse From(Webhook webhook, bool revealSecret)
    {
        return new WebhookResponse(
            webhook.Id,
            webhook.UserId,
            webhook.EventTypeId,
            webhook.TargetUrl,
            revealSecret ? webhook.Secret : MaskSecret(webhook.Secret),
            webhook.EventSubscriptions.ToArray(),
            webhook.Active,
            webhook.CreatedAt
        );
    }

    private static string MaskSecret(string secret)
    {
        var last4 = secret.Length >= 4 ? secret[^4..] : secret;
        return $"{SecretMaskPrefix}{SecretMaskFiller}-{last4}";
    }
}

[PublicAPI]
public sealed record WebhooksResponse(WebhookResponse[] Webhooks);

[PublicAPI]
public sealed record TestWebhookResponse(WebhookDeliveryId DeliveryId);
