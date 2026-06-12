using System.Globalization;
using System.Text.Json;
using Main.Features.Autonomy.Domain;
using Main.Features.Autonomy.Shared;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Autonomy.Jobs;

/// <summary>
///     The payment-recovery ladder (spec R31, design §A2): finds bookings whose deposit has been pending
///     for over two hours with no reminder sent, and nudges the customer on WhatsApp with the original
///     payment link. Deterministic detection and copy; the existing release job still frees the slot if
///     payment never arrives.
/// </summary>
public sealed class PaymentRecoveryJob(
    IBookingRepository bookingRepository,
    IWhatsAppBusinessAccountRepository businessAccountRepository,
    IWhatsAppOutboundSender outboundSender,
    TimeProvider timeProvider
) : IAutonomyJob
{
    private static readonly TimeSpan ReminderDelay = TimeSpan.FromHours(2);

    public string JobType => AutonomyJobTypes.PaymentRecovery;

    public int DefaultLevel => 1;

    public async Task<AutonomyDetection[]> DetectAsync(TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - ReminderDelay;
        var pendingBookings = await bookingRepository.GetPendingPaymentsForReminderUnfilteredAsync(cutoff, cancellationToken);

        return pendingBookings
            .Where(booking => booking.TenantId == tenantId && booking.BookerPhone is not null && booking.PaymentLinkUrl is not null)
            .Select(booking => new AutonomyDetection(
                    booking.Id.Value,
                    $"{booking.BookerName} has not paid the deposit for {booking.Title} on {FormatLocal(booking.StartTime, booking.TimeZone)}. Send a friendly payment reminder?",
                    JsonSerializer.Serialize(new PaymentRecoveryPayload(booking.Id.Value))
                )
            )
            .ToArray();
    }

    public async Task<Result<string>> ExecuteAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PaymentRecoveryPayload>(jobRun.PayloadJson ?? "{}");
        if (payload?.BookingId is null)
        {
            return Result<string>.BadRequest("The job payload is missing the booking.");
        }

        var booking = await bookingRepository.GetByIdUnfilteredAsync(new BookingId(payload.BookingId), cancellationToken);
        if (booking is null || booking.TenantId != jobRun.TenantId)
        {
            return Result<string>.NotFound("The booking no longer exists.");
        }

        if (booking.PaymentStatus != BookingPaymentStatus.Pending || booking.BookerPhone is null || booking.PaymentLinkUrl is null)
        {
            return Result<string>.BadRequest("The deposit is no longer outstanding.");
        }

        var account = await businessAccountRepository.GetByTenantIdUnfilteredAsync(jobRun.TenantId, cancellationToken);
        if (account is null)
        {
            return Result<string>.BadRequest("WhatsApp is not connected for this business.");
        }

        var message = $"Hi {FirstNameOf(booking.BookerName)}! Just a friendly reminder to secure your {booking.Title} on {FormatLocal(booking.StartTime, booking.TimeZone)} — your booking is confirmed as soon as the deposit is paid: {booking.PaymentLinkUrl}";
        var sent = await outboundSender.SendTextAsync(account, booking.BookerPhone, message, cancellationToken);
        if (!sent)
        {
            return Result<string>.BadRequest("The WhatsApp reminder could not be sent.");
        }

        booking.MarkPaymentReminderSent(timeProvider.GetUtcNow());
        bookingRepository.Update(booking);

        return Result<string>.Success($"Reminded {booking.BookerName} about the unpaid deposit for {booking.Title} on {FormatLocal(booking.StartTime, booking.TimeZone)}.");
    }

    private static string FirstNameOf(string fullName)
    {
        var spaceIndex = fullName.IndexOf(' ');
        return spaceIndex < 0 ? fullName : fullName[..spaceIndex];
    }

    private static string FormatLocal(DateTimeOffset utcTime, string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utcTime, timeZoneId).ToString("ddd d MMM 'at' HH:mm", CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcTime.ToString("ddd d MMM 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }
    }

    private sealed record PaymentRecoveryPayload(string BookingId);
}
