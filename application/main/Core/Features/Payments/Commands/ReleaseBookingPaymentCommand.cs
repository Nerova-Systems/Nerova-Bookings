using JetBrains.Annotations;
using Main.Features.Payments.Domain;
using Main.Features.Payments.Infrastructure;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Payments.Commands;

/// <summary>
///     Records a Paystack payment failure and releases the booking slot (cancels it). Dispatched
///     by the webhook for <c>charge.failed</c>, or by the unpaid-booking polling job once the hold
///     window expires.
/// </summary>
[PublicAPI]
public sealed record ReleaseBookingPaymentCommand(string Reference, string EventId) : ICommand, IRequest<Result>;

public sealed class ReleaseBookingPaymentHandler(
    IBookingRepository bookingRepository,
    IProcessedPaymentEventRepository processedPaymentEventRepository,
    TimeProvider timeProvider,
    ILogger<ReleaseBookingPaymentHandler> logger
) : IRequestHandler<ReleaseBookingPaymentCommand, Result>
{
    public async Task<Result> Handle(ReleaseBookingPaymentCommand command, CancellationToken cancellationToken)
    {
        if (await processedPaymentEventRepository.IsProcessedAsync(command.EventId, cancellationToken))
        {
            return Result.Success();
        }

        var booking = await bookingRepository.GetByPaymentReferenceUnfilteredAsync(command.Reference, cancellationToken);
        if (booking is null)
        {
            logger.LogWarning("Paystack charge.failed received for unknown reference {Reference}", command.Reference);
            await processedPaymentEventRepository.AddAsync(ProcessedPaymentEvent.Create(command.EventId, timeProvider.GetUtcNow()), cancellationToken);
            return Result.Success();
        }

        var now = timeProvider.GetUtcNow();
        booking.MarkPaymentFailed(now);
        booking.ReleaseForUnpaidPayment(now);
        bookingRepository.Update(booking);

        await processedPaymentEventRepository.AddAsync(ProcessedPaymentEvent.Create(command.EventId, now), cancellationToken);
        return Result.Success();
    }
}
