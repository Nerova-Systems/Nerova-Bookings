using System.Globalization;
using JetBrains.Annotations;
using Main.Features.Payments.Paystack;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using SharedKernel.Persistence;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Post-flow messaging fan-out. Triggered by <see cref="BookingCreatedViaFlowEvent" /> after
///     a booking is created via the WhatsApp Flow. Responsibilities:
///     <list type="bullet">
///         <item>Render the tenant's confirmation template and WhatsApp it to the booker.</item>
///         <item>
///             When <see cref="PaymentTiming.BeforeBooking" /> + a Paystack subaccount is configured,
///             initialise a Paystack payment link, transition the booking to
///             <see cref="BookingPaymentStatus.Pending" />, and WhatsApp the link to the booker.
///         </item>
///     </list>
///     All failures are logged not thrown — the booking has already been created, so we never
///     surface infrastructure failures back to the flow caller.
/// </summary>
[UsedImplicitly]
public sealed class PostFlowMessagesDispatcher(
    IBookingRepository bookingRepository,
    ITenantFlowConfigRepository tenantFlowConfigRepository,
    IWhatsAppFlowProfileSync flowProfileSync,
    IWhatsAppCloudApiClient whatsAppClient,
    IPaystackPaymentLinkService paystackPaymentLinkService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<PostFlowMessagesDispatcher> logger
) : INotificationHandler<BookingCreatedViaFlowEvent>
{
    public async Task Handle(BookingCreatedViaFlowEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var booking = await bookingRepository.GetByIdUnfilteredAsync(notification.BookingId, cancellationToken);
            if (booking is null)
            {
                logger.LogWarning("PostFlowMessagesDispatcher: booking {BookingId} not found", notification.BookingId);
                return;
            }

            var config = await tenantFlowConfigRepository.GetByTenantIdAsync(notification.TenantId, cancellationToken);
            if (config is null)
            {
                logger.LogWarning("PostFlowMessagesDispatcher: TenantFlowConfig not found for tenant {TenantId}", notification.TenantId);
                return;
            }

            var profile = await flowProfileSync.GetByTenantId(notification.TenantId, cancellationToken);
            if (profile is null || string.IsNullOrWhiteSpace(profile.PhoneNumberId) || string.IsNullOrWhiteSpace(profile.WabaAccessToken))
            {
                logger.LogWarning("PostFlowMessagesDispatcher: WABA credentials missing for tenant {TenantId}", notification.TenantId);
                return;
            }

            await SendConfirmationSummary(profile, booking, notification.BookerWaId, notification.BookerName, config.ConfirmationMessageTemplate, cancellationToken);

            // Pre-booking payment: triggered for both BeforeBooking (full) and Deposit (partial)
            // timings. AfterSession is handled by BookingCompletedNotificationHandler.
            if (config.PaymentTiming is not (PaymentTiming.BeforeBooking or PaymentTiming.Deposit))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.PaystackSubaccountCode))
            {
                logger.LogInformation("PostFlowMessagesDispatcher: PaymentTiming = BeforeBooking but no Paystack subaccount configured for tenant {TenantId}", notification.TenantId);
                return;
            }

            await DispatchPaymentLink(profile, booking, config, notification.BookerWaId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Never throw — the booking has already been created and a thrown exception here would
            // propagate up through IPublisher.Publish back into the ConfirmBookingScreenHandler,
            // which would then surface an error to the booker even though the booking succeeded.
            logger.LogError(ex, "PostFlowMessagesDispatcher: unexpected failure for booking {BookingId}", notification.BookingId);
        }
    }

    private async Task SendConfirmationSummary(
        WhatsAppFlowProfile profile,
        Booking booking,
        string bookerWaId,
        string bookerName,
        string template,
        CancellationToken cancellationToken)
    {
        var body = RenderTemplate(template, bookerName, booking);
        try
        {
            await whatsAppClient.SendTextMessageAsync(profile.PhoneNumberId!, profile.WabaAccessToken!, bookerWaId, body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PostFlowMessagesDispatcher: failed to send confirmation summary for booking {BookingId}", booking.Id);
        }
    }

    private async Task DispatchPaymentLink(
        WhatsAppFlowProfile profile,
        Booking booking,
        TenantFlowConfig config,
        string bookerWaId,
        CancellationToken cancellationToken)
    {
        var amountMinor = config.DepositAmountCents.GetValueOrDefault(0);
        if (amountMinor <= 0)
        {
            logger.LogWarning("PostFlowMessagesDispatcher: tenant {TenantId} has no DepositAmountCents configured; skipping payment link", profile.TenantId);
            return;
        }

        var currency = configuration["Paystack:DefaultCurrency"] ?? "NGN";
        var reference = $"book-{booking.Id.Value}-{timeProvider.GetUtcNow().ToUnixTimeSeconds()}";

        PaystackPaymentLink? link;
        try
        {
            link = await paystackPaymentLinkService.CreatePaymentLinkAsync(
                profile.PaystackSubaccountCode!,
                amountMinor,
                currency,
                booking.BookerEmail,
                reference,
                callbackUrl: null,
                metadata: new Dictionary<string, string> { ["booking_id"] = booking.Id.Value },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PostFlowMessagesDispatcher: Paystack initialise threw for booking {BookingId}", booking.Id);
            return;
        }

        if (link is null)
        {
            logger.LogWarning("PostFlowMessagesDispatcher: Paystack returned no link for booking {BookingId}", booking.Id);
            return;
        }

        booking.MarkPaymentPending(link.Reference, link.AuthorizationUrl, timeProvider.GetUtcNow());
        bookingRepository.Update(booking);
        await unitOfWork.CommitAsync(cancellationToken);

        try
        {
            var body = $"Please complete your payment to confirm your booking: {link.AuthorizationUrl}";
            await whatsAppClient.SendTextMessageAsync(profile.PhoneNumberId!, profile.WabaAccessToken!, bookerWaId, body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PostFlowMessagesDispatcher: failed to send payment link for booking {BookingId}", booking.Id);
        }
    }

    private static string RenderTemplate(string template, string bookerName, Booking booking)
    {
        var effective = string.IsNullOrWhiteSpace(template)
            ? "Hi {name}, your booking on {time} is confirmed."
            : template;

        var startLocal = booking.StartTime.ToString("ddd dd MMM HH:mm", CultureInfo.InvariantCulture);

        return effective
            .Replace("{name}", bookerName, StringComparison.Ordinal)
            .Replace("{service}", string.Empty, StringComparison.Ordinal)
            .Replace("{staff}", string.Empty, StringComparison.Ordinal)
            .Replace("{time}", startLocal, StringComparison.Ordinal);
    }
}
