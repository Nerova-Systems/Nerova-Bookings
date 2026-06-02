using System.Text.Json;
using Main.Database;
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

                        foreach (var msg in value.Messages)
                        {
                            if (string.IsNullOrEmpty(msg.Id) || string.IsNullOrEmpty(msg.From)) continue;

                            // Dedup: if a message with this MetaMessageId already exists, skip
                            var existing = await whatsAppMessageRepository.GetByMetaMessageIdUnfilteredAsync(msg.Id, cancellationToken);
                            if (existing is not null) continue;

                            if (account is null)
                            {
                                logger.LogWarning("Received WhatsApp message for unknown phone number ID '{PhoneNumberId}', skipping", phoneNumberId);
                                continue;
                            }

                            var toPhoneNumber = value.Metadata?.DisplayPhoneNumber ?? string.Empty;
                            var timestamp = DateTimeOffset.FromUnixTimeSeconds(long.TryParse(msg.Timestamp, out var ts) ? ts : 0);
                            var text = msg.Text?.Body ?? string.Empty;

                            var message = WhatsAppMessage.CreateInbound(account.TenantId, msg.Id, msg.From, toPhoneNumber, text, timestamp);
                            await whatsAppMessageRepository.AddAsync(message, cancellationToken);
                            events.CollectEvent(new WhatsAppMessageReceived(message.Id));
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

    private sealed record MetaWebhookPayload(MetaWebhookEntry[]? Entry);

    private sealed record MetaWebhookEntry(string? Id, MetaWebhookChange[]? Changes);

    private sealed record MetaWebhookChange(MetaWebhookValue? Value);

    private sealed record MetaWebhookValue(
        MetaWebhookMetadata? Metadata,
        MetaWebhookMessage[]? Messages,
        MetaWebhookStatus[]? Statuses
    );

    private sealed record MetaWebhookMetadata(string? PhoneNumberId, string? DisplayPhoneNumber);

    private sealed record MetaWebhookMessage(string? Id, string? From, string? Timestamp, MetaWebhookMessageText? Text);

    private sealed record MetaWebhookMessageText(string? Body);

    private sealed record MetaWebhookStatus(string? Id, string? Status);
}
