using Main.Features.WhatsAppMessaging.Domain;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Integrations.Meta;

namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     Sends outbound WhatsApp messages on behalf of a tenant's connected WhatsApp Business Account and records
///     each one as a <see cref="WhatsAppMessage" /> so the conversation transcript stays complete. Never throws.
/// </summary>
public interface IWhatsAppOutboundSender
{
    Task<bool> SendTextAsync(WhatsAppBusinessAccount account, string toPhoneNumber, string text, CancellationToken cancellationToken);

    Task<bool> SendButtonsAsync(WhatsAppBusinessAccount account, string toPhoneNumber, string bodyText, IReadOnlyList<WhatsAppReplyButton> buttons, CancellationToken cancellationToken);

    Task<bool> SendFlowAsync(
        WhatsAppBusinessAccount account,
        string toPhoneNumber,
        string bodyText,
        string flowId,
        string flowToken,
        string flowCtaText,
        string? initialScreen,
        object? initialData,
        CancellationToken cancellationToken
    );
}

/// <summary>
///     Sends outbound WhatsApp messages on behalf of a tenant's connected WhatsApp Business Account and records
///     each one as a <see cref="WhatsAppMessage" /> so the conversation transcript stays complete for the debug
///     console. Resolves and unprotects the per-tenant access token. Never throws — returns false on any failure.
/// </summary>
public sealed class WhatsAppOutboundSender(
    IWhatsAppMessageRepository messageRepository,
    MetaGraphClientFactory metaGraphClientFactory,
    WhatsAppAccessTokenProtector accessTokenProtector,
    TimeProvider timeProvider,
    ILogger<WhatsAppOutboundSender> logger
) : IWhatsAppOutboundSender
{
    public Task<bool> SendTextAsync(WhatsAppBusinessAccount account, string toPhoneNumber, string text, CancellationToken cancellationToken)
    {
        return SendAsync(
            account,
            toPhoneNumber,
            text,
            (client, accessToken, phoneNumberId) => client.SendTextMessageAsync(phoneNumberId, accessToken, toPhoneNumber, text, cancellationToken),
            cancellationToken
        );
    }

    public Task<bool> SendButtonsAsync(WhatsAppBusinessAccount account, string toPhoneNumber, string bodyText, IReadOnlyList<WhatsAppReplyButton> buttons, CancellationToken cancellationToken)
    {
        return SendAsync(
            account,
            toPhoneNumber,
            bodyText,
            (client, accessToken, phoneNumberId) => client.SendInteractiveButtonsAsync(phoneNumberId, accessToken, toPhoneNumber, bodyText, buttons, cancellationToken),
            cancellationToken
        );
    }

    public Task<bool> SendFlowAsync(
        WhatsAppBusinessAccount account,
        string toPhoneNumber,
        string bodyText,
        string flowId,
        string flowToken,
        string flowCtaText,
        string? initialScreen,
        object? initialData,
        CancellationToken cancellationToken
    )
    {
        return SendAsync(
            account,
            toPhoneNumber,
            bodyText,
            (client, accessToken, phoneNumberId) => client.SendFlowMessageAsync(
                phoneNumberId,
                accessToken,
                toPhoneNumber,
                bodyText,
                flowId,
                flowToken,
                flowCtaText,
                initialScreen,
                initialData,
                cancellationToken
            ),
            cancellationToken
        );
    }

    private async Task<bool> SendAsync(
        WhatsAppBusinessAccount account,
        string toPhoneNumber,
        string transcriptText,
        Func<IMetaGraphClient, string, string, Task<string?>> send,
        CancellationToken cancellationToken
    )
    {
        var accessToken = accessTokenProtector.Unprotect(account.AccessToken);
        if (accessToken is null)
        {
            logger.LogWarning("Cannot send WhatsApp message for tenant {TenantId}: the access token could not be unprotected.", account.TenantId);
            return false;
        }

        var client = metaGraphClientFactory.GetClient();
        var metaMessageId = await send(client, accessToken, account.PhoneNumber.MetaPhoneNumberId);
        if (metaMessageId is null)
        {
            return false;
        }

        var message = WhatsAppMessage.CreateOutbound(
            account.TenantId,
            metaMessageId,
            account.PhoneNumber.DisplayPhoneNumber,
            toPhoneNumber,
            transcriptText,
            timeProvider.GetUtcNow()
        );
        await messageRepository.AddAsync(message, cancellationToken);
        return true;
    }
}
