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
///     The rebook job (design §A2): when a booking was cancelled in the last three days and the customer
///     has nothing else on the calendar, invite them on WhatsApp to pick a new time. One run per
///     cancelled booking; capped per customer by the trigger-uniqueness guarantee.
/// </summary>
public sealed class RebookCancelledJob(
    IBookingRepository bookingRepository,
    IWhatsAppBusinessAccountRepository businessAccountRepository,
    IWhatsAppOutboundSender outboundSender,
    TimeProvider timeProvider
) : IAutonomyJob
{
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromHours(72);

    public string JobType => AutonomyJobTypes.RebookCancelled;

    public int DefaultLevel => 1;

    public async Task<AutonomyDetection[]> DetectAsync(TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cancelledBookings = await bookingRepository.GetRecentlyCancelledUnfilteredAsync(tenantId, now - LookbackWindow, cancellationToken);
        if (cancelledBookings.Length == 0) return [];

        var detections = new List<AutonomyDetection>();
        foreach (var booking in cancelledBookings.Where(b => b.BookerPhone is not null))
        {
            var upcoming = await bookingRepository.GetUpcomingByBookerPhoneUnfilteredAsync(tenantId, booking.BookerPhone!, now, cancellationToken);
            if (upcoming.Length > 0) continue;

            detections.Add(new AutonomyDetection(
                    booking.Id.Value,
                    $"{booking.BookerName} cancelled {booking.Title} and has nothing rebooked. Invite them to pick a new time?",
                    JsonSerializer.Serialize(new RebookPayload(booking.Id.Value))
                )
            );
        }

        return [.. detections];
    }

    public async Task<Result<string>> ExecuteAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<RebookPayload>(jobRun.PayloadJson ?? "{}");
        if (payload?.BookingId is null)
        {
            return Result<string>.BadRequest("The job payload is missing the booking.");
        }

        var booking = await bookingRepository.GetByIdUnfilteredAsync(new BookingId(payload.BookingId), cancellationToken);
        if (booking?.BookerPhone is null || booking.TenantId != jobRun.TenantId)
        {
            return Result<string>.NotFound("The booking no longer exists.");
        }

        var upcoming = await bookingRepository.GetUpcomingByBookerPhoneUnfilteredAsync(jobRun.TenantId, booking.BookerPhone, timeProvider.GetUtcNow(), cancellationToken);
        if (upcoming.Length > 0)
        {
            return Result<string>.BadRequest("The customer has already rebooked.");
        }

        var account = await businessAccountRepository.GetByTenantIdUnfilteredAsync(jobRun.TenantId, cancellationToken);
        if (account is null)
        {
            return Result<string>.BadRequest("WhatsApp is not connected for this business.");
        }

        var message = $"Hi {FirstNameOf(booking.BookerName)}! We're sorry your {booking.Title} didn't work out. Just reply here whenever you'd like to pick a new time — we'll sort it out in seconds.";
        var sent = await outboundSender.SendTextAsync(account, booking.BookerPhone, message, cancellationToken);
        if (!sent)
        {
            return Result<string>.BadRequest("The WhatsApp invitation could not be sent.");
        }

        return Result<string>.Success($"Invited {booking.BookerName} to rebook after their cancelled {booking.Title}.");
    }

    private static string FirstNameOf(string fullName)
    {
        var spaceIndex = fullName.IndexOf(' ');
        return spaceIndex < 0 ? fullName : fullName[..spaceIndex];
    }

    private sealed record RebookPayload(string BookingId);
}
