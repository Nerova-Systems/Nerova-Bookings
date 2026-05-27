using JetBrains.Annotations;
using Main.Features.Payments.Paystack;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using SharedKernel.Persistence;

namespace Main.Features.Scheduling.Notifications;

/// <summary>
///     Handles <see cref="BookingCompletedNotification" /> for tenants configured with
///     <see cref="PaymentTiming.AfterSession" />: creates a Paystack payment link, transitions
///     the booking to <see cref="BookingPaymentStatus.Pending" />, and WhatsApps the booker.
///     For other payment timings this is a no-op. Failures are logged not thrown.
/// </summary>
[UsedImplicitly]
public sealed class BookingCompletedNotificationHandler(
    IBookingRepository bookingRepository,
    ITenantFlowConfigRepository tenantFlowConfigRepository,
    IWhatsAppFlowProfileSync flowProfileSync,
    IWhatsAppCloudApiClient whatsAppClient,
    IPaystackPaymentLinkService paystackPaymentLinkService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<BookingCompletedNotificationHandler> logger
) : INotificationHandler<BookingCompletedNotification>
{
    public async Task Handle(BookingCompletedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var booking = await bookingRepository.GetByIdUnfilteredAsync(notification.BookingId, cancellationToken);
            if (booking is null)
            {
                logger.LogWarning("BookingCompleted: booking {BookingId} not found", notification.BookingId);
                return;
            }

            // Skip if a payment is already in flight or settled for this booking.
            if (booking.PaymentStatus is BookingPaymentStatus.Pending or BookingPaymentStatus.Paid)
            {
                return;
            }

            var config = await tenantFlowConfigRepository.GetByTenantIdAsync(notification.TenantId, cancellationToken);
            if (config is null || config.PaymentTiming != PaymentTiming.AfterSession)
            {
                return;
            }

            var profile = await flowProfileSync.GetByTenantId(notification.TenantId, cancellationToken);
            if (profile is null
                || string.IsNullOrWhiteSpace(profile.PhoneNumberId)
                || string.IsNullOrWhiteSpace(profile.WabaAccessToken)
                || string.IsNullOrWhiteSpace(profile.PaystackSubaccountCode))
            {
                logger.LogWarning("BookingCompleted: WABA/Paystack credentials missing for tenant {TenantId}", notification.TenantId);
                return;
            }

            var amountMinor = config.DepositAmountCents.GetValueOrDefault(0);
            if (amountMinor <= 0)
            {
                logger.LogWarning("BookingCompleted: tenant {TenantId} has no DepositAmountCents configured", notification.TenantId);
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
                logger.LogWarning(ex, "BookingCompleted: Paystack initialise threw for booking {BookingId}", booking.Id);
                return;
            }

            if (link is null)
            {
                return;
            }

            booking.MarkPaymentPending(link.Reference, link.AuthorizationUrl, timeProvider.GetUtcNow());
            bookingRepository.Update(booking);
            await unitOfWork.CommitAsync(cancellationToken);

            try
            {
                var bookerWaId = booking.BookerEmail; // No phone on Booking; callers provide WaId on flow path only
                var body = $"Thanks for visiting! Please complete your payment: {link.AuthorizationUrl}";
                await whatsAppClient.SendTextMessageAsync(profile.PhoneNumberId!, profile.WabaAccessToken!, bookerWaId, body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "BookingCompleted: failed to send payment link for booking {BookingId}", booking.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BookingCompleted: unexpected failure for booking {BookingId}", notification.BookingId);
        }
    }
}
