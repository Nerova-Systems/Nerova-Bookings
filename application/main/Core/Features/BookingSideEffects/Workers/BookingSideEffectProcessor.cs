using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Main.Database;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.Connectors.Domain;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Integrations.Email;

namespace Main.Features.BookingSideEffects.Workers;

public sealed class BookingSideEffectProcessor(
    MainDbContext mainDbContext,
    IEmailClient emailClient,
    IHttpClientFactory httpClientFactory,
    ICoreConnectorClient coreConnectorClient,
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
        mainDbContext.ChangeTracker.Clear();
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
            else if (delivery.Kind.Equals(BookingSideEffectConstants.CalendarKind, StringComparison.OrdinalIgnoreCase))
            {
                await SyncCalendarAsync(delivery, cancellationToken);
            }
            else if (delivery.Kind.Equals(BookingSideEffectConstants.ConferencingKind, StringComparison.OrdinalIgnoreCase))
            {
                await SyncConferencingAsync(delivery, cancellationToken);
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

    private async Task SyncCalendarAsync(BookingSideEffectDelivery delivery, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BookingConnectorCalendarDeliveryPayload>(delivery.PayloadJson, JsonSerializerOptions)
                      ?? throw new JsonException("Calendar connector delivery payload is invalid.");
        var booking = await LoadBookingAsync(delivery.BookingId, cancellationToken);
        var destinationCalendar = new EventTypeDestinationCalendar
        {
            Integration = payload.Integration,
            ExternalId = payload.ExternalId,
            CredentialId = payload.CredentialId
        };
        var conferencing = string.IsNullOrWhiteSpace(payload.ConferencingApp)
            ? null
            : new EventTypeDefaultConferencing
            {
                App = payload.ConferencingApp,
                CredentialId = payload.ConferencingCredentialId
            };
        var operation = ResolveConnectorOperation(payload.Operation, delivery.Trigger);

        if (operation.Equals(BookingSideEffectConstants.DeleteOperation, StringComparison.OrdinalIgnoreCase))
        {
            await coreConnectorClient.DeleteCalendarEventAsync(booking, destinationCalendar, cancellationToken);
            booking.MarkReferencesDeleted(destinationCalendar.Integration);
            return;
        }

        var reference = operation.Equals(BookingSideEffectConstants.UpdateOperation, StringComparison.OrdinalIgnoreCase)
            ? await coreConnectorClient.UpdateCalendarEventAsync(booking, destinationCalendar, conferencing, cancellationToken)
            : await coreConnectorClient.CreateCalendarEventAsync(booking, destinationCalendar, conferencing, cancellationToken);
        booking.UpsertReference(reference);
    }

    private async Task SyncConferencingAsync(BookingSideEffectDelivery delivery, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BookingConnectorConferencingDeliveryPayload>(delivery.PayloadJson, JsonSerializerOptions)
                      ?? throw new JsonException("Conferencing connector delivery payload is invalid.");
        var booking = await LoadBookingAsync(delivery.BookingId, cancellationToken);
        var conferencing = new EventTypeDefaultConferencing { App = payload.App, CredentialId = payload.CredentialId };
        var operation = ResolveConnectorOperation(payload.Operation, delivery.Trigger);

        if (operation.Equals(BookingSideEffectConstants.DeleteOperation, StringComparison.OrdinalIgnoreCase))
        {
            await coreConnectorClient.DeleteMeetingAsync(booking, conferencing, cancellationToken);
            booking.MarkReferencesDeleted(conferencing.App);
            return;
        }

        var reference = operation.Equals(BookingSideEffectConstants.UpdateOperation, StringComparison.OrdinalIgnoreCase)
            ? await coreConnectorClient.UpdateMeetingAsync(booking, conferencing, cancellationToken)
            : await coreConnectorClient.CreateMeetingAsync(booking, conferencing, cancellationToken);
        booking.UpsertReference(reference);
    }

    private async Task<Booking> LoadBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await mainDbContext.Set<Booking>()
                   .IgnoreQueryFilters()
                   .AsTracking()
                   .SingleOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken) ??
               throw new JsonException($"Booking '{bookingId}' was not found for connector delivery.");
    }

    private static string ResolveConnectorOperation(string? operation, string trigger)
    {
        if (!string.IsNullOrWhiteSpace(operation))
        {
            return operation.Trim().ToLowerInvariant();
        }

        return trigger switch
        {
            BookingSideEffectConstants.BookingCreated => BookingSideEffectConstants.CreateOperation,
            BookingSideEffectConstants.BookingConfirmed => BookingSideEffectConstants.UpdateOperation,
            BookingSideEffectConstants.BookingLocationChanged => BookingSideEffectConstants.UpdateOperation,
            BookingSideEffectConstants.BookingGuestsAdded => BookingSideEffectConstants.UpdateOperation,
            BookingSideEffectConstants.BookingRejected => BookingSideEffectConstants.DeleteOperation,
            BookingSideEffectConstants.BookingCancelled => BookingSideEffectConstants.DeleteOperation,
            BookingSideEffectConstants.BookingRescheduled => BookingSideEffectConstants.DeleteOperation,
            _ => BookingSideEffectConstants.UpdateOperation
        };
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
