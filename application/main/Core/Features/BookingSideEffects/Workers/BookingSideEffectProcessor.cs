using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Main.Database;
using Main.Features.BookingSideEffects.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Integrations.Email;

namespace Main.Features.BookingSideEffects.Workers;

public sealed class BookingSideEffectProcessor(
    MainDbContext mainDbContext,
    IEmailClient emailClient,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<BookingSideEffectProcessor> logger
)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var pendingDeliveries = await mainDbContext.Set<BookingSideEffectDelivery>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(delivery => delivery.Status == BookingSideEffectConstants.PendingStatus)
            .ToArrayAsync(cancellationToken);
        var deliveries = pendingDeliveries
            .Where(delivery => delivery.NextRetryAt == null || delivery.NextRetryAt <= now)
            .OrderBy(delivery => delivery.CreatedAt)
            .ThenBy(delivery => delivery.Id)
            .Take(batchSize)
            .ToArray();

        foreach (var delivery in deliveries)
        {
            await ProcessDeliveryAsync(delivery, cancellationToken);
        }

        await mainDbContext.SaveChangesAsync(cancellationToken);
        return deliveries.Length;
    }

    private async Task ProcessDeliveryAsync(BookingSideEffectDelivery delivery, CancellationToken cancellationToken)
    {
        try
        {
            if (delivery.Kind.Equals(BookingSideEffectConstants.EmailKind, StringComparison.OrdinalIgnoreCase))
            {
                await SendEmailAsync(delivery, cancellationToken);
            }
            else if (delivery.Kind.Equals(BookingSideEffectConstants.WebhookKind, StringComparison.OrdinalIgnoreCase))
            {
                await SendWebhookAsync(delivery, cancellationToken);
            }
            else
            {
                delivery.MarkFailed($"Unsupported delivery kind '{delivery.Kind}'.", timeProvider.GetUtcNow());
                return;
            }

            delivery.MarkSent();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Booking side-effect delivery failed for {DeliveryId}", delivery.Id);
            delivery.MarkFailed(exception.Message, timeProvider.GetUtcNow());
        }
    }

    private async Task SendEmailAsync(BookingSideEffectDelivery delivery, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BookingEmailDeliveryPayload>(delivery.PayloadJson, JsonSerializerOptions)
                      ?? throw new JsonException("Email delivery payload is invalid.");
        var subject = string.IsNullOrWhiteSpace(payload.Subject) ? DefaultEmailSubject(payload) : payload.Subject.Trim();
        var plainTextBody = string.IsNullOrWhiteSpace(payload.Body) ? DefaultEmailBody(payload) : payload.Body.Trim();

        await emailClient.SendAsync(
            new EmailMessage(
                payload.BookerEmail,
                subject,
                plainTextBody.Replace(Environment.NewLine, "<br>", StringComparison.Ordinal),
                plainTextBody,
                new Dictionary<string, string> { ["X-Cal-Com-Trigger"] = payload.Trigger }
            ),
            cancellationToken
        );
    }

    private async Task SendWebhookAsync(BookingSideEffectDelivery delivery, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BookingWebhookDeliveryPayload>(delivery.PayloadJson, JsonSerializerOptions)
                      ?? throw new JsonException("Webhook delivery payload is invalid.");
        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, payload.SubscriberUrl);
        request.Content = JsonContent.Create(new
            {
                triggerEvent = payload.Trigger,
                createdAt = timeProvider.GetUtcNow(),
                payload = new
                {
                    uid = payload.BookingId,
                    type = payload.EventTitle,
                    title = payload.EventTitle,
                    startTime = payload.StartTime,
                    endTime = payload.EndTime,
                    status = payload.Status,
                    attendees = new[] { new { name = payload.BookerName, email = payload.BookerEmail } },
                    location = payload.LocationValue
                }
            }
        );

        request.Headers.Add("X-Cal-Event", payload.Trigger);
        request.Headers.Add("X-Cal-Webhook-Version", payload.PayloadVersion);
        if (!string.IsNullOrWhiteSpace(payload.Secret))
        {
            request.Headers.Add("X-Cal-Signature-256", SignPayload(delivery.PayloadJson, payload.Secret));
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string DefaultEmailSubject(BookingEmailDeliveryPayload payload)
    {
        var action = payload.Trigger switch
        {
            BookingSideEffectConstants.BookingConfirmed => "Booking confirmed",
            BookingSideEffectConstants.BookingRejected => "Booking rejected",
            BookingSideEffectConstants.BookingCancelled => "Booking cancelled",
            BookingSideEffectConstants.BookingRescheduled => "Booking rescheduled",
            BookingSideEffectConstants.BookingLocationChanged => "Booking location changed",
            BookingSideEffectConstants.BookingGuestsAdded => "Booking guests updated",
            _ => "Booking created"
        };
        return $"{action} for {payload.EventTitle}";
    }

    private static string DefaultEmailBody(BookingEmailDeliveryPayload payload)
    {
        return $"""
                {payload.BookerName},

                {payload.EventTitle} is {payload.Status}.
                Start: {payload.StartTime:u}
                End: {payload.EndTime:u}
                """;
    }

    private static string SignPayload(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var hash = new HMACSHA256(key);
        return $"sha256={Convert.ToHexString(hash.ComputeHash(bytes)).ToLowerInvariant()}";
    }
}
