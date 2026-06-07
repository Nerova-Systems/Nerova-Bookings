namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     Configuration for the WhatsApp booking conversation engine. Bound from environment variables by the
///     AppHost.
/// </summary>
public sealed class WhatsAppBookingOptions
{
    public const string SectionName = "WhatsAppBooking";

    /// <summary>
    ///     The published WhatsApp Flow id used to capture booking details (service, date, time, contact).
    ///     When empty, the engine falls back to a plain-text greeting so the inbound/outbound loop still works
    ///     before the Flow has been published and configured.
    /// </summary>
    public string? FlowId { get; set; }

    /// <summary>
    ///     The published WhatsApp Flow id used for customer login / registration (name + email + OTP).
    ///     When empty, the engine falls back to a plain-text prompt asking the customer to text their email.
    /// </summary>
    public string? LoginFlowId { get; set; }
}
