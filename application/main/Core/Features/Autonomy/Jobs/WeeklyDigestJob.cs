using System.Globalization;
using System.Text;
using Main.Features.Autonomy.Domain;
using Main.Features.Autonomy.Shared;
using Main.Features.Clients.Domain;
using Main.Features.Receptionist.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using Microsoft.Extensions.AI;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Autonomy.Jobs;

/// <summary>
///     The owner's weekly business summary (spec R26/R27): deterministic metrics — bookings, deposits,
///     cancellations, new and at-risk clients — narrated by the owner agent in two friendly sentences.
///     The model narrates; it never computes numbers, and a deterministic fallback narration ships when
///     no model is configured. One run per ISO week per tenant; delivered to the owner's WhatsApp when a
///     number is configured, and always visible in the "Handled by Nerova" feed.
/// </summary>
public sealed class WeeklyDigestJob(
    IBookingRepository bookingRepository,
    IClientRepository clientRepository,
    IReceptionistSettingsRepository receptionistSettingsRepository,
    IWhatsAppBusinessAccountRepository businessAccountRepository,
    IWhatsAppOutboundSender outboundSender,
    IChatClient chatClient,
    TimeProvider timeProvider,
    ILogger<WeeklyDigestJob> logger
) : IAutonomyJob
{
    public string JobType => AutonomyJobTypes.WeeklyDigest;

    public int DefaultLevel => 2;

    public Task<AutonomyDetection[]> DetectAsync(TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Fires once per ISO week per tenant: the week id is the trigger, so re-detection is a no-op.
        var isoWeek = $"{ISOWeek.GetYear(now.UtcDateTime)}-W{ISOWeek.GetWeekOfYear(now.UtcDateTime):00}";
        AutonomyDetection[] detections = [new(isoWeek, "Weekly business summary", null)];
        return Task.FromResult(detections);
    }

    public async Task<Result<string>> ExecuteAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var weekStart = now.AddDays(-7);

        var weekBookings = await bookingRepository.GetForTenantRangeUnfilteredAsync(jobRun.TenantId, weekStart, now, cancellationToken);
        var clients = await clientRepository.GetAllForDuplicateCheckUnfilteredAsync(jobRun.TenantId, cancellationToken);

        var totalBookings = weekBookings.Length;
        var cancelledBookings = weekBookings.Count(booking => booking.Status == BookingStatus.Cancelled);
        var depositsCollected = weekBookings.Count(booking => booking.PaymentStatus == BookingPaymentStatus.Paid);
        var newClients = clients.Count(client => client.CreatedAt >= weekStart);
        var atRiskClients = clients.Count(client => client.LastVisitAt is not null && client.LastVisitAt < now.AddDays(-42));

        var metricsSummary = $"This week: {totalBookings} bookings ({cancelledBookings} cancelled), {depositsCollected} deposits collected, {newClients} new clients, {atRiskClients} clients at risk of lapsing (no visit in 6+ weeks).";

        var narrative = await NarrateAsync(metricsSummary, cancellationToken);
        var receipt = $"{narrative}\n{metricsSummary}";

        var settings = await receptionistSettingsRepository.GetByTenantUnfilteredAsync(jobRun.TenantId, cancellationToken);
        if (settings?.OwnerPhoneNumber is not null)
        {
            var account = await businessAccountRepository.GetByTenantIdUnfilteredAsync(jobRun.TenantId, cancellationToken);
            if (account is not null)
            {
                await outboundSender.SendTextAsync(account, settings.OwnerPhoneNumber, receipt, cancellationToken);
            }
        }

        return Result<string>.Success(receipt);
    }

    private async Task<string> NarrateAsync(string metricsSummary, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("You write a two-sentence upbeat weekly summary for a small appointment-business owner in South Africa.");
            prompt.AppendLine("Use ONLY the numbers provided — never invent or recompute. No greetings, no markdown.");
            prompt.AppendLine(metricsSummary);

            var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt.ToString())], new ChatOptions { MaxOutputTokens = 200 }, cancellationToken);
            var narrative = response.Text.Trim();
            if (narrative.Length > 0 && !narrative.StartsWith("Thanks for your message", StringComparison.Ordinal))
            {
                return narrative;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Weekly digest narration failed; using deterministic copy");
        }

        return "Here's how your week went — your front desk handled it all.";
    }
}
