using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using Main.Features.WhatsAppMessaging.Domain;
using Microsoft.Extensions.Configuration;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppMessaging.Commands;

/// <summary>
///     Phase 1 of two-phase WhatsApp webhook processing. Verifies the Meta signature, deduplicates
///     using a SHA-256 hash of the raw body, persists the event as Pending, and returns the new event
///     for phase 2. Returns null when the payload is a duplicate (already processed).
/// </summary>
[PublicAPI]
public sealed record AcknowledgeWhatsAppWebhookCommand(string Payload, string SignatureHeader) : ICommand, IRequest<Result<WhatsAppEvent?>>;

public sealed class AcknowledgeWhatsAppWebhookHandler(
    IWhatsAppEventRepository whatsAppEventRepository,
    IConfiguration configuration
) : IRequestHandler<AcknowledgeWhatsAppWebhookCommand, Result<WhatsAppEvent?>>
{
    public async Task<Result<WhatsAppEvent?>> Handle(AcknowledgeWhatsAppWebhookCommand command, CancellationToken cancellationToken)
    {
        var appSecret = configuration["Meta:AppSecret"] ?? string.Empty;

        // Verify HMAC-SHA256 signature: expected = "sha256=" + hex(HMAC-SHA256(body, appSecret))
        var expectedSignature = ComputeExpectedSignature(command.Payload, appSecret);
        var providedSignature = command.SignatureHeader.Trim();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedSignature),
                Encoding.ASCII.GetBytes(providedSignature)))
        {
            return Result<WhatsAppEvent?>.Unauthorized("Invalid webhook signature.");
        }

        // Dedup: use SHA-256 of the raw body as MetaEventId
        var metaEventId = ComputePayloadHash(command.Payload);

        var isDuplicate = await whatsAppEventRepository.ExistsAsync(metaEventId, cancellationToken);
        if (isDuplicate)
        {
            return Result<WhatsAppEvent?>.Success(null);
        }

        var whatsAppEvent = WhatsAppEvent.Create(metaEventId, command.Payload);
        await whatsAppEventRepository.AddAsync(whatsAppEvent, cancellationToken);

        return Result<WhatsAppEvent?>.Success(whatsAppEvent);
    }

    private static string ComputeExpectedSignature(string payload, string appSecret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputePayloadHash(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
