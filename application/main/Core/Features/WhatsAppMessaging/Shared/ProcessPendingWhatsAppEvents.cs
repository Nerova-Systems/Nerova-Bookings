using System.Text.Json;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.WhatsAppBooking;
using Main.Features.WhatsAppBooking.Shared;
using Main.Features.WhatsAppMessaging.Domain;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.WhatsAppMessaging.Shared;

/// <summary>
///     Phase 2 of two-phase WhatsApp webhook processing. Parses the raw payload stored in the
///     <see cref="WhatsAppEvent" /> inbox, upserts <see cref="WhatsAppMessage" /> rows, and
///     transitions the event to Processed or Failed.
/// </summary>
public sealed class ProcessPendingWhatsAppEvents(
    MainDbContext dbContext,
    IWhatsAppEventRepository whatsAppEventRepository,
    IWhatsAppMessageRepository whatsAppMessageRepository,
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    WhatsAppConversationEngine conversationEngine,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    ILogger<ProcessPendingWhatsAppEvents> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task ExecuteAsync(WhatsAppEvent whatsAppEvent, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(whatsAppEvent.Payload, JsonOptions);
            if (payload?.Entry is null)
            {
                whatsAppEvent.MarkProcessed(timeProvider.GetUtcNow());
                whatsAppEventRepository.Update(whatsAppEvent);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            foreach (var entry in payload.Entry)
            {
                if (entry.Changes is null) continue;
                foreach (var change in entry.Changes)
                {
                    var value = change.Value;
                    if (value is null) continue;

                    var phoneNumberId = value.Metadata?.PhoneNumberId;

                    // Process inbound messages
                    if (value.Messages is not null)
                    {
                        var account = phoneNumberId is not null
                            ? await whatsAppBusinessAccountRepository.GetByMetaPhoneNumberIdUnfilteredAsync(phoneNumberId, cancellationToken)
                            : null;

                        if (account is null)
                        {
                            var error = $"No WABA registered for Meta phone number ID '{phoneNumberId}'. Re-run embedded signup or check WABA configuration.";
                            logger.LogError("WhatsApp event '{EventId}': {Error}", whatsAppEvent.Id.Value, error);
                            whatsAppEvent.MarkFailed(timeProvider.GetUtcNow(), error);
                            whatsAppEventRepository.Update(whatsAppEvent);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            return;
                        }

                        foreach (var msg in value.Messages)
                        {
                            if (string.IsNullOrEmpty(msg.Id) || string.IsNullOrEmpty(msg.From)) continue;

                            // Dedup: if a message with this MetaMessageId already exists, skip
                            var existing = await whatsAppMessageRepository.GetByMetaMessageIdUnfilteredAsync(msg.Id, cancellationToken);
                            if (existing is not null) continue;

                            var toPhoneNumber = value.Metadata?.DisplayPhoneNumber ?? string.Empty;
                            var timestamp = DateTimeOffset.FromUnixTimeSeconds(long.TryParse(msg.Timestamp, out var ts) ? ts : 0);
                            var text = ResolveInboundText(msg);

                            var message = WhatsAppMessage.CreateInbound(account.TenantId, msg.Id, msg.From, toPhoneNumber, text, timestamp);
                            await whatsAppMessageRepository.AddAsync(message, cancellationToken);
                            events.CollectEvent(new WhatsAppMessageReceived(message.Id));

                            // Drive the deterministic booking conversation (greet, launch Flow, advance state).
                            var inbound = BuildInboundMessage(msg.From, msg);
                            await conversationEngine.HandleInboundAsync(account, inbound, cancellationToken);
                        }
                    }

                    // Process status updates
                    if (value.Statuses is not null)
                    {
                        foreach (var status in value.Statuses)
                        {
                            if (string.IsNullOrEmpty(status.Id) || string.IsNullOrEmpty(status.Status)) continue;

                            var existingMessage = await whatsAppMessageRepository.GetByMetaMessageIdUnfilteredAsync(status.Id, cancellationToken);
                            if (existingMessage is null) continue;

                            var newStatus = ParseStatus(status.Status);
                            if (newStatus is null) continue;

                            existingMessage.UpdateStatus(newStatus.Value);
                            whatsAppMessageRepository.Update(existingMessage);
                        }
                    }
                }
            }

            whatsAppEvent.MarkProcessed(timeProvider.GetUtcNow());
            whatsAppEventRepository.Update(whatsAppEvent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process WhatsApp event '{EventId}'", whatsAppEvent.Id.Value);
            whatsAppEvent.MarkFailed(timeProvider.GetUtcNow(), ex.Message);
            whatsAppEventRepository.Update(whatsAppEvent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static WhatsAppMessageStatus? ParseStatus(string status)
    {
        return status switch
        {
            "delivered" => WhatsAppMessageStatus.Delivered,
            "read" => WhatsAppMessageStatus.Read,
            "failed" => WhatsAppMessageStatus.Failed,
            "sent" => WhatsAppMessageStatus.Sent,
            _ => null
        };
    }

    /// <summary>
    ///     Resolves a human-readable transcript representation of an inbound message. Plain text returns its
    ///     body; an interactive button/list reply returns the tapped option's title; a Flow completion
    ///     (nfm_reply) returns the submitted response JSON. The full raw payload always remains available on the
    ///     <see cref="WhatsAppEvent" /> for deep debugging.
    /// </summary>
    private static string ResolveInboundText(MetaWebhookMessage message)
    {
        if (message.Text?.Body is { Length: > 0 } body)
        {
            return body;
        }

        var interactive = message.Interactive;
        if (interactive?.ButtonReply?.Title is { Length: > 0 } buttonTitle)
        {
            return buttonTitle;
        }

        if (interactive?.ListReply?.Title is { Length: > 0 } listTitle)
        {
            return listTitle;
        }

        if (interactive?.NfmReply?.ResponseJson is { Length: > 0 } responseJson)
        {
            return responseJson;
        }

        return string.Empty;
    }

    /// <summary>
    ///     Maps the parsed Meta webhook message to the normalized inbound shape the conversation engine consumes:
    ///     a Flow completion (with its submitted response JSON), an interactive button/list reply (with the
    ///     tapped option id), plain text, or an unsupported message type.
    /// </summary>
    private static WhatsAppInboundMessage BuildInboundMessage(string fromPhoneNumber, MetaWebhookMessage message)
    {
        var interactive = message.Interactive;
        if (interactive?.NfmReply is not null)
        {
            return new WhatsAppInboundMessage(fromPhoneNumber, WhatsAppInboundKind.FlowCompletion, null, null, interactive.NfmReply.ResponseJson);
        }

        if (interactive?.ButtonReply is not null)
        {
            return new WhatsAppInboundMessage(fromPhoneNumber, WhatsAppInboundKind.ButtonReply, interactive.ButtonReply.Title, interactive.ButtonReply.Id, null);
        }

        if (interactive?.ListReply is not null)
        {
            return new WhatsAppInboundMessage(fromPhoneNumber, WhatsAppInboundKind.ListReply, interactive.ListReply.Title, interactive.ListReply.Id, null);
        }

        if (message.Text?.Body is { Length: > 0 } body)
        {
            return new WhatsAppInboundMessage(fromPhoneNumber, WhatsAppInboundKind.Text, body, null, null);
        }

        return new WhatsAppInboundMessage(fromPhoneNumber, WhatsAppInboundKind.Other, null, null, null);
    }

    private sealed record MetaWebhookPayload(MetaWebhookEntry[]? Entry);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookEntry(string? Id, MetaWebhookChange[]? Changes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookChange(MetaWebhookValue? Value);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookValue(
        MetaWebhookMetadata? Metadata,
        MetaWebhookMessage[]? Messages,
        MetaWebhookStatus[]? Statuses
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookMetadata(string? PhoneNumberId, string? DisplayPhoneNumber);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookMessage(string? Id, string? From, string? Timestamp, string? Type, MetaWebhookMessageText? Text, MetaWebhookInteractive? Interactive);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookMessageText(string? Body);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookInteractive(
        string? Type,
        MetaWebhookInteractiveReply? ButtonReply,
        MetaWebhookInteractiveReply? ListReply,
        MetaWebhookNfmReply? NfmReply
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookInteractiveReply(string? Id, string? Title, string? Description);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookNfmReply(string? Name, string? Body, string? ResponseJson);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MetaWebhookStatus(string? Id, string? Status);
}
