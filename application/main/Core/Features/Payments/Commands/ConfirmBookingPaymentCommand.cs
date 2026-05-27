using JetBrains.Annotations;
using Main.Features.Payments.Domain;
using Main.Features.Payments.Infrastructure;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Payments.Commands;

/// <summary>
///     Marks the booking matching <paramref name="Reference" /> as paid. Dispatched by the
///     Paystack webhook handler for <c>charge.success</c> events. No permission attribute: the
///     webhook is anonymous and idempotency is enforced by the <see cref="ProcessedPaymentEvent" />
///     table written alongside the booking update.
/// </summary>
[PublicAPI]
public sealed record ConfirmBookingPaymentCommand(string Reference, string EventId) : ICommand, IRequest<Result>;

public sealed class ConfirmBookingPaymentHandler(
    IBookingRepository bookingRepository,
    IProcessedPaymentEventRepository processedPaymentEventRepository,
    TimeProvider timeProvider,
    ILogger<ConfirmBookingPaymentHandler> logger
) : IRequestHandler<ConfirmBookingPaymentCommand, Result>
{
    public async Task<Result> Handle(ConfirmBookingPaymentCommand command, CancellationToken cancellationToken)
    {
        if (await processedPaymentEventRepository.IsProcessedAsync(command.EventId, cancellationToken))
        {
            // Paystack retries until 2xx — a duplicate delivery is a no-op success.
            return Result.Success();
        }

        var booking = await bookingRepository.GetByPaymentReferenceUnfilteredAsync(command.Reference, cancellationToken);
        if (booking is null)
        {
            // Unknown reference: still mark the event as processed so we don't retry forever, but
            // log so operators can spot rogue or test payments routed to production webhooks.
            logger.LogWarning("Paystack charge.success received for unknown reference {Reference}", command.Reference);
            await processedPaymentEventRepository.AddAsync(ProcessedPaymentEvent.Create(command.EventId, timeProvider.GetUtcNow()), cancellationToken);
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow();
        booking.MarkPaymentPaid(now);
        bookingRepository.Update(booking);

        await processedPaymentEventRepository.AddAsync(ProcessedPaymentEvent.Create(command.EventId, now), cancellationToken);
        return Result.Success();
    }
}
