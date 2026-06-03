using System.Text.RegularExpressions;
using Main.Database;
using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Senders;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Integrations.Email;
using SharedKernel.Persistence;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Workflows.Jobs;

/// <summary>
///     Dispatches due workflow reminders via email, SMS, or WhatsApp.
///     Runs every 60 seconds and processes up to 100 reminders per run.
///     <para>
///         SMS / WhatsApp transient failures (network, 5xx, 429) bump <c>RetryCount</c> and leave
///         the reminder Pending for a future tick. Permanent failures (4xx) or exceeding
///         <see cref="MaxRetries" /> move the reminder to Failed.
///     </para>
/// </summary>
public sealed class DispatchWorkflowReminderJob(
    IWorkflowReminderRepository reminderRepository,
    MainDbContext dbContext,
    IEmailClient emailClient,
    ISmsProvider smsProvider,
    IWhatsAppProvider whatsAppProvider,
    IHostEmailProvider hostEmailProvider,
    IUnitOfWork unitOfWork,
    ILogger<DispatchWorkflowReminderJob> logger
) : ITickerFunction
{
    private const int BatchSize = 100;

    /// <summary>
    ///     Maximum number of dispatch attempts before a reminder is marked Failed.
    ///     Mirrors cal.com's three-strike policy for SMS/WhatsApp reminders.
    /// </summary>
    public const int MaxRetries = 3;

    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dueReminders = await reminderRepository.GetPendingDueAsync(now, BatchSize, ct);

        if (dueReminders.Length == 0) return;

        var bookingIds = dueReminders.Select(r => r.BookingId).Distinct().ToList();
        var bookings = await dbContext.Set<Booking>()
            .IgnoreQueryFilters()
            .Where(b => bookingIds.Contains(b.Id))
            .ToArrayAsync(ct);

        var bookingById = bookings.ToDictionary(b => b.Id);

        foreach (var reminder in dueReminders)
        {
            if (!bookingById.TryGetValue(reminder.BookingId, out var booking))
            {
                reminder.MarkCancelled();
                continue;
            }

            try
            {
                await DispatchReminderAsync(reminder, booking, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled error dispatching workflow reminder {ReminderId} for booking {BookingId}",
                    reminder.Id.Value,
                    reminder.BookingId.Value
                );
                reminder.RecordFailedAttempt(ex.Message, MaxRetries);
            }
        }

        reminderRepository.UpdateRange(dueReminders);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task DispatchReminderAsync(WorkflowReminder reminder, Booking booking, CancellationToken ct)
    {
        switch (reminder.Action)
        {
            case WorkflowAction.EmailHost:
                await SendEmailToHostAsync(reminder, booking, ct);
                break;

            case WorkflowAction.EmailAttendee:
                await SendEmailAsync(reminder, booking.BookerEmail, reminder.EmailSubject ?? string.Empty, reminder.EmailBody ?? string.Empty, ct);
                break;

            case WorkflowAction.EmailAddress when !string.IsNullOrWhiteSpace(reminder.SendTo):
                await SendEmailAsync(reminder, reminder.SendTo, reminder.EmailSubject ?? string.Empty, reminder.EmailBody ?? string.Empty, ct);
                break;

            case WorkflowAction.SmsNumber when !string.IsNullOrWhiteSpace(reminder.SendTo):
                await SendSmsAsync(reminder, reminder.SendTo, ct);
                break;

            case WorkflowAction.WhatsappNumber when !string.IsNullOrWhiteSpace(reminder.SendTo):
                await SendWhatsAppAsync(reminder, booking.TenantId, reminder.SendTo, ct);
                break;

            case WorkflowAction.SmsAttendee:
            case WorkflowAction.WhatsappAttendee:
                // Booking does not capture an attendee phone number yet. Until that field lands
                // (deferred), *Attendee SMS/WhatsApp reminders cannot be delivered — cancel cleanly.
                logger.LogWarning(
                    "Workflow reminder {ReminderId} action {Action} cannot be dispatched: attendee phone number not captured at booking time.",
                    reminder.Id.Value,
                    reminder.Action
                );
                reminder.MarkCancelled();
                break;

            default:
                logger.LogWarning(
                    "Workflow reminder {ReminderId} action {Action} cannot be dispatched (missing SendTo or unsupported action).",
                    reminder.Id.Value,
                    reminder.Action
                );
                reminder.MarkCancelled();
                break;
        }
    }

    private async Task SendSmsAsync(WorkflowReminder reminder, string phoneE164, CancellationToken ct)
    {
        var result = await smsProvider.SendAsync(phoneE164, reminder.EmailBody ?? string.Empty, ct);
        switch (result.Status)
        {
            case SmsResultStatus.Sent:
                reminder.MarkDispatched(result.MessageId);
                break;

            case SmsResultStatus.NotConfigured:
                // Don't burn a retry on missing config — cancel cleanly so the queue does not loop.
                logger.LogWarning(
                    "Skipping SMS reminder {ReminderId} — provider not configured: {Reason}.",
                    reminder.Id.Value,
                    result.ErrorReason
                );
                reminder.MarkCancelled();
                break;

            case SmsResultStatus.TransientFailure:
                reminder.RecordFailedAttempt(result.ErrorReason ?? "transient", MaxRetries);
                break;

            case SmsResultStatus.PermanentFailure:
                reminder.MarkFailed(result.ErrorReason ?? "permanent");
                break;
        }
    }

    private async Task SendWhatsAppAsync(WorkflowReminder reminder, TenantId tenantId, string phoneE164, CancellationToken ct)
    {
        var templateName = ResolveTemplateName(reminder.Template);
        var variables = BuildWhatsAppVariables(reminder);

        var result = await whatsAppProvider.SendAsync(tenantId, phoneE164, templateName, variables, ct);
        switch (result.Status)
        {
            case WhatsAppResultStatus.Sent:
                reminder.MarkDispatched(result.MessageId);
                break;

            case WhatsAppResultStatus.NotConfigured:
                logger.LogWarning(
                    "Skipping WhatsApp reminder {ReminderId} — provider not configured: {Reason}.",
                    reminder.Id.Value,
                    result.ErrorReason
                );
                reminder.MarkCancelled();
                break;

            case WhatsAppResultStatus.TransientFailure:
                reminder.RecordFailedAttempt(result.ErrorReason ?? "transient", MaxRetries);
                break;

            case WhatsAppResultStatus.PermanentFailure:
                reminder.MarkFailed(result.ErrorReason ?? "permanent");
                break;
        }
    }

    private static string ResolveTemplateName(WorkflowReminderTemplate template)
    {
        return template switch
        {
            WorkflowReminderTemplate.Reminder => "booking_reminder",
            WorkflowReminderTemplate.RatingRequest => "booking_rating_request",
            WorkflowReminderTemplate.ThankYou => "booking_thank_you",
            WorkflowReminderTemplate.Custom => "booking_custom",
            _ => "booking_reminder"
        };
    }

    private static IReadOnlyDictionary<string, string> BuildWhatsAppVariables(WorkflowReminder reminder)
    {
        // Meta-approved templates use positional {{1}}, {{2}}, ... placeholders. We expose the
        // rendered reminder body as {{1}} and the booking start (ISO 8601 UTC) as {{2}} so a
        // single approved template can serve all reminder kinds.
        return new Dictionary<string, string>
        {
            ["1"] = reminder.EmailBody ?? string.Empty,
            ["2"] = reminder.BookingStartTime.ToString("u")
        };
    }

    private async Task SendEmailToHostAsync(WorkflowReminder reminder, Booking booking, CancellationToken ct)
    {
        var hostEmail = await hostEmailProvider.GetEmailAsync(booking.OwnerUserId, ct);
        if (hostEmail is null)
        {
            logger.LogWarning(
                "Skipping EmailHost reminder {ReminderId}: host email not resolvable for user {UserId}.",
                reminder.Id.Value,
                booking.OwnerUserId.Value
            );
            reminder.MarkCancelled();
            return;
        }

        await SendEmailAsync(reminder, hostEmail, reminder.EmailSubject ?? string.Empty, reminder.EmailBody ?? string.Empty, ct);
    }

    private async Task SendEmailAsync(WorkflowReminder reminder, string recipient, string subject, string body, CancellationToken ct)
    {
        var message = new EmailMessage(
            recipient,
            subject,
            body,
            StripHtml(body)
        );

        await emailClient.SendAsync(message, ct);
        reminder.MarkDispatched();
    }

    private static string StripHtml(string html)
    {
        return Regex.Replace(html, "<[^>]*>", string.Empty);
    }
}
