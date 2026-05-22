using Main.Database;
using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Senders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Integrations.Email;
using SharedKernel.Persistence;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Workflows.Jobs;

/// <summary>
///     Dispatches due workflow reminders via email, SMS, or WhatsApp.
///     Runs every 60 seconds and processes up to 100 reminders per run.
/// </summary>
public sealed class DispatchWorkflowReminderJob(
    IWorkflowReminderRepository reminderRepository,
    MainDbContext dbContext,
    IEmailClient emailClient,
    ISmsSender smsSender,
    IWhatsappSender whatsappSender,
    IHostEmailProvider hostEmailProvider,
    IUnitOfWork unitOfWork,
    ILogger<DispatchWorkflowReminderJob> logger
) : ITickerFunction
{
    private const int BatchSize = 100;

    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dueReminders = await reminderRepository.GetPendingDueAsync(now, BatchSize, ct);

        if (dueReminders.Length == 0) return;

        var bookingIds = dueReminders.Select(r => r.BookingId).Distinct().ToArray();
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
                    "Failed to dispatch workflow reminder {ReminderId} for booking {BookingId}",
                    reminder.Id.Value,
                    reminder.BookingId.Value
                );
                reminder.MarkFailed(ex.Message);
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

            case WorkflowAction.SmsAttendee:
                var smsRef = await smsSender.SendAsync(booking.BookerEmail, reminder.EmailBody ?? string.Empty, ct);
                reminder.MarkDispatched(smsRef);
                break;

            case WorkflowAction.SmsNumber when !string.IsNullOrWhiteSpace(reminder.SendTo):
                var smsNumRef = await smsSender.SendAsync(reminder.SendTo, reminder.EmailBody ?? string.Empty, ct);
                reminder.MarkDispatched(smsNumRef);
                break;

            case WorkflowAction.WhatsappAttendee:
                var waRef = await whatsappSender.SendAsync(booking.BookerEmail, reminder.EmailBody ?? string.Empty, ct);
                reminder.MarkDispatched(waRef);
                break;

            case WorkflowAction.WhatsappNumber when !string.IsNullOrWhiteSpace(reminder.SendTo):
                var waNumRef = await whatsappSender.SendAsync(reminder.SendTo, reminder.EmailBody ?? string.Empty, ct);
                reminder.MarkDispatched(waNumRef);
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
            Recipient: recipient,
            Subject: subject,
            HtmlBody: body,
            PlainTextBody: StripHtml(body)
        );

        await emailClient.SendAsync(message, ct);
        reminder.MarkDispatched();
    }

    private static string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", string.Empty);
    }
}
