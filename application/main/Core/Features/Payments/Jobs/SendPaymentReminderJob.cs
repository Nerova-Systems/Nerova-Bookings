using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using SharedKernel.Persistence;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Payments.Jobs;

/// <summary>
///     Polls for bookings with a pending payment whose payment-state-changed timestamp is older
///     than <see cref="ReminderWindow" /> (default 48 hours) and which have not yet been
///     reminded. Sends a WhatsApp text nudge and stamps
///     <see cref="Booking.PaymentReminderSentAt" /> as a re-entrancy guard. Cron-poll pattern
///     mirrors the other TickerQ jobs in this codebase.
/// </summary>
public sealed class SendPaymentReminderJob(
    IBookingRepository bookingRepository,
    IWhatsAppFlowProfileSync flowProfileSync,
    IWhatsAppCloudApiClient whatsAppClient,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork,
    ILogger<SendPaymentReminderJob> logger
) : ITickerFunction
{
    /// <summary>Gap between payment-link dispatch and reminder send.</summary>
    public static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(48);

    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        _ = context;
        var now = timeProvider.GetUtcNow();
        var cutoff = now - ReminderWindow;
        var due = await bookingRepository.GetPendingPaymentsForReminderUnfilteredAsync(cutoff, ct);
        if (due.Length == 0) return;

        var anyMarked = false;
        foreach (var booking in due)
        {
            if (ct.IsCancellationRequested) break;

            var profile = await flowProfileSync.GetByTenantId(booking.TenantId, ct);
            if (profile is null || string.IsNullOrWhiteSpace(profile.PhoneNumberId) || string.IsNullOrWhiteSpace(profile.WabaAccessToken))
            {
                logger.LogWarning("SendPaymentReminderJob: WABA credentials missing for tenant {TenantId}", booking.TenantId);
                continue;
            }

            try
            {
                var body = $"Friendly reminder: your booking payment is still pending. Please complete it here: {booking.PaymentLinkUrl}";
                // No phone on Booking — callers send to BookerEmail as the to-id when WaId unknown.
                await whatsAppClient.SendTextMessageAsync(profile.PhoneNumberId!, profile.WabaAccessToken!, booking.BookerEmail, body, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SendPaymentReminderJob: failed to send reminder for booking {BookingId}", booking.Id);
                continue;
            }

            booking.MarkPaymentReminderSent(now);
            bookingRepository.Update(booking);
            anyMarked = true;
        }

        if (anyMarked)
        {
            await unitOfWork.CommitAsync(ct);
        }
    }
}
