using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Payments.Paystack;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

[PublicAPI]
public sealed record RequestBookingDepositResponse(string PaymentUrl, decimal Amount, string Currency);

/// <summary>
///     Initialises a Paystack checkout for a booking's deposit (spec R5): computes the deposit from the
///     event type's payment settings, creates the hosted payment link routed to the tenant's subaccount,
///     and marks the booking payment Pending. The booking is confirmed to the customer only after the
///     <c>charge.success</c> webhook flips <see cref="BookingPaymentStatus" /> to Paid; the existing
///     release job frees the slot if payment never arrives.
/// </summary>
[PublicAPI]
public sealed record RequestBookingDepositCommand(TenantId TenantId, BookingId BookingId, string? CustomerPhoneNumber = null)
    : ICommand, IRequest<Result<RequestBookingDepositResponse>>;

public sealed class RequestBookingDepositHandler(
    IBookingRepository bookingRepository,
    IEventTypeRepository eventTypeRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IPaystackPaymentLinkService paystackPaymentLinkService,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<RequestBookingDepositCommand, Result<RequestBookingDepositResponse>>
{
    public async Task<Result<RequestBookingDepositResponse>> Handle(RequestBookingDepositCommand command, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdUnfilteredAsync(command.BookingId, cancellationToken);
        if (booking is null || booking.TenantId != command.TenantId)
        {
            return Result<RequestBookingDepositResponse>.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        if (command.CustomerPhoneNumber is not null && booking.BookerPhone != command.CustomerPhoneNumber)
        {
            return Result<RequestBookingDepositResponse>.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        if (booking.PaymentStatus == BookingPaymentStatus.Paid)
        {
            return Result<RequestBookingDepositResponse>.BadRequest("The deposit for this booking has already been paid.");
        }

        if (booking.PaymentStatus == BookingPaymentStatus.Pending && booking.PaymentLinkUrl is not null)
        {
            // Idempotent: re-requesting returns the existing link instead of creating a second charge.
            var eventTypeForAmount = await eventTypeRepository.GetByIdUnfilteredAsync(command.TenantId, booking.EventTypeId, cancellationToken);
            var existingAmount = eventTypeForAmount?.Settings.Payment.DepositAmount ?? 0;
            var existingCurrency = eventTypeForAmount?.Settings.Payment.Currency ?? "ZAR";
            return Result<RequestBookingDepositResponse>.Success(new RequestBookingDepositResponse(booking.PaymentLinkUrl, existingAmount, existingCurrency));
        }

        var eventType = await eventTypeRepository.GetByIdUnfilteredAsync(command.TenantId, booking.EventTypeId, cancellationToken);
        if (eventType is null)
        {
            return Result<RequestBookingDepositResponse>.NotFound($"Booking '{command.BookingId}' was not found.");
        }

        var payment = eventType.Settings.Payment;
        if (!payment.RequiresDeposit)
        {
            return Result<RequestBookingDepositResponse>.BadRequest("This service does not require a deposit.");
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (profile?.PaystackSubaccountCode is null)
        {
            return Result<RequestBookingDepositResponse>.BadRequest("Payments are not set up for this business yet.");
        }

        var amountMinorUnits = (long)(payment.DepositAmount!.Value * 100);
        var reference = $"dep_{booking.Id.Value}";
        var paymentLink = await paystackPaymentLinkService.CreatePaymentLinkAsync(
            profile.PaystackSubaccountCode,
            amountMinorUnits,
            payment.Currency,
            booking.BookerEmail,
            reference,
            null,
            new Dictionary<string, string> { ["booking_id"] = booking.Id.Value, ["kind"] = "deposit" },
            cancellationToken
        );

        if (paymentLink is null)
        {
            return Result<RequestBookingDepositResponse>.BadRequest("The payment link could not be created. Please try again shortly.");
        }

        booking.MarkPaymentPending(paymentLink.Reference, paymentLink.AuthorizationUrl, timeProvider.GetUtcNow());
        bookingRepository.Update(booking);

        events.CollectEvent(new BookingDepositRequested(booking.Id, eventType.Id, amountMinorUnits));

        return Result<RequestBookingDepositResponse>.Success(new RequestBookingDepositResponse(paymentLink.AuthorizationUrl, payment.DepositAmount.Value, payment.Currency));
    }
}
