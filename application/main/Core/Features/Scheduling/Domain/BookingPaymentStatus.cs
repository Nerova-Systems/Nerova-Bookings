using JetBrains.Annotations;

namespace Main.Features.Scheduling.Domain;

/// <summary>
///     Payment lifecycle for bookings that flow through the WhatsApp Flows + Paystack pipeline.
///     <list type="bullet">
///         <item><see cref="NotRequired" /> — no payment expected (default for legacy bookings).</item>
///         <item><see cref="Pending" /> — payment link dispatched, awaiting Paystack confirmation.</item>
///         <item><see cref="Paid" /> — Paystack <c>charge.success</c> received.</item>
///         <item><see cref="Failed" /> — Paystack <c>charge.failed</c> received.</item>
///         <item><see cref="Released" /> — slot released because payment did not arrive in time.</item>
///     </list>
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingPaymentStatus
{
    NotRequired,
    Pending,
    Paid,
    Failed,
    Released
}
