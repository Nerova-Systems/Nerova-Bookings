using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Raised by <see cref="ConfirmBookingScreenHandler" /> after a public booking is created
///     through the WhatsApp Flow. The <c>PostFlowMessagesDispatcher</c> consumes this notification
///     to send the booker a confirmation summary and (when payment-timing is BeforeBooking) a
///     Paystack payment link.
///
///     Plain <see cref="INotification" /> (not <see cref="SharedKernel.DomainEvents.IDomainEvent" />)
///     so the handler is free to perform external I/O (Paystack/WhatsApp calls) — domain events
///     in this project run inside the unit-of-work pipeline and must remain side-effect free.
/// </summary>
[PublicAPI]
public sealed record BookingCreatedViaFlowEvent(
    BookingId BookingId,
    TenantId TenantId,
    string BookerWaId,
    string BookerName
) : INotification;
