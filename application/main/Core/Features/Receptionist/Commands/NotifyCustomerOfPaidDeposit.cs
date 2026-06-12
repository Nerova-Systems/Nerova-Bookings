using JetBrains.Annotations;
using Main.Features.Receptionist.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     The payment-resume turn (spec R5): when Paystack confirms a deposit, the customer receives the
///     booking confirmation on WhatsApp without sending a new message. Dispatched by the payment webhook
///     path after <see cref="BookingPaymentStatus" /> flips to Paid. Deterministic copy — no model call
///     is needed to say "you're booked".
/// </summary>
[PublicAPI]
public sealed record NotifyCustomerOfPaidDepositCommand(BookingId BookingId) : ICommand, IRequest<Result>;

public sealed class NotifyCustomerOfPaidDepositHandler(
    IBookingRepository bookingRepository,
    IReceptionistSettingsRepository receptionistSettingsRepository,
    IWhatsAppBusinessAccountRepository businessAccountRepository,
    IWhatsAppConversationRepository conversationRepository,
    IWhatsAppOutboundSender outboundSender,
    ITelemetryEventsCollector events
) : IRequestHandler<NotifyCustomerOfPaidDepositCommand, Result>
{
    public async Task<Result> Handle(NotifyCustomerOfPaidDepositCommand command, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdUnfilteredAsync(command.BookingId, cancellationToken);
        if (booking?.BookerPhone is null)
        {
            return Result.Success();
        }

        var settings = await receptionistSettingsRepository.GetByTenantUnfilteredAsync(booking.TenantId, cancellationToken);
        if (settings?.IsEnabled != true)
        {
            return Result.Success();
        }

        var conversation = await conversationRepository.GetByTenantAndPhoneUnfilteredAsync(booking.TenantId, booking.BookerPhone, cancellationToken);
        if (conversation is null)
        {
            return Result.Success();
        }

        var account = await businessAccountRepository.GetByTenantIdUnfilteredAsync(booking.TenantId, cancellationToken);
        if (account is null)
        {
            return Result.Success();
        }

        var localStart = FormatLocal(booking.StartTime, booking.TimeZone);
        var confirmation = $"Deposit received — you're booked for {localStart}. See you then! We'll send a reminder before your appointment.";
        await outboundSender.SendTextAsync(account, booking.BookerPhone, confirmation, cancellationToken);

        events.CollectEvent(new DepositCollectedByAgent(booking.Id));

        return Result.Success();
    }

    private static string FormatLocal(DateTimeOffset utcTime, string timeZoneId)
    {
        try
        {
            var local = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utcTime, timeZoneId);
            return local.ToString("dddd d MMMM 'at' HH:mm");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcTime.ToString("dddd d MMMM 'at' HH:mm 'UTC'");
        }
    }
}
