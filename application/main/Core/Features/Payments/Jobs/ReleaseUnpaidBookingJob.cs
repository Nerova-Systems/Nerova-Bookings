using Main.Features.Scheduling.Domain;
using SharedKernel.Persistence;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Payments.Jobs;

/// <summary>
///     Polls for bookings whose payment-pending hold has exceeded the configured timeout
///     (default 30 minutes from <see cref="Booking.PaymentStateChangedAt" />) and releases the
///     slot via <see cref="Booking.ReleaseForUnpaidPayment" />.
///     Implemented as a cron-poll job (the only TickerQ pattern in this codebase) rather than a
///     schedule-with-input job. The webhook-driven path
///     (<c>ReleaseBookingPaymentCommand</c>) is reserved for explicit Paystack
///     <c>charge.expired</c> events; this job covers the case where no terminal webhook arrives.
/// </summary>
public sealed class ReleaseUnpaidBookingJob(
    IBookingRepository bookingRepository,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork,
    ILogger<ReleaseUnpaidBookingJob> logger
) : ITickerFunction
{
    /// <summary>Hold window: bookings older than this without payment are released.</summary>
    public static readonly TimeSpan HoldWindow = TimeSpan.FromMinutes(30);

    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        _ = context;
        var now = timeProvider.GetUtcNow();
        var cutoff = now - HoldWindow;
        var expired = await bookingRepository.GetExpiredUnpaidBookingsUnfilteredAsync(cutoff, ct);
        if (expired.Length == 0) return;

        foreach (var booking in expired)
        {
            if (ct.IsCancellationRequested) break;
            booking.ReleaseForUnpaidPayment(now);
            bookingRepository.Update(booking);
            logger.LogInformation("ReleaseUnpaidBookingJob: released booking {BookingId} (state changed {ChangedAt})", booking.Id, booking.PaymentStateChangedAt);
        }

        await unitOfWork.CommitAsync(ct);
    }
}
